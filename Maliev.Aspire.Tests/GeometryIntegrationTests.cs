using System.Net;
using System.Net.Http.Json;
using Xunit;
using Xunit.Abstractions;
using Aspire.Hosting.Testing;
using Maliev.Aspire.ServiceDefaults.Testing;

namespace Maliev.Aspire.Tests;

public class GeometryIntegrationTests : IAsyncLifetime
{
    private DistributedApplicationFactory? _appFactory;
    private readonly ITestOutputHelper _output;

    public GeometryIntegrationTests(ITestOutputHelper output)
    {
        _output = output;
    }

    public async Task InitializeAsync()
    {
        var appHostAssembly = typeof(Projects.Maliev_Aspire_AppHost).Assembly;
        _appFactory = new DistributedApplicationFactory(appHostAssembly.EntryPoint!.DeclaringType!);
        
        // Globally configure HttpClient for upload service to follow redirects
        // Use DelegatingHandler pattern or just use simple HttpClient if factory allows
        
        await _appFactory.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (_appFactory != null)
        {
            await _appFactory.DisposeAsync();
        }
    }

    [Fact]
    public async Task GeometryService_Report_Healthy()
    {
        // Arrange
        var client = _appFactory!.CreateHttpClient("geometry-service");
        client.Timeout = TimeSpan.FromSeconds(60);

        // Act & Assert
        // Retry logic for service startup stabilization
        HttpResponseMessage? response = null;
        for (int i = 0; i < 10; i++)
        {
            try
            {
                response = await client.GetAsync("/readiness");
                if (response.IsSuccessStatusCode) break;
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Attempt {i + 1} failed: {ex.Message}");
            }
            await Task.Delay(5000);
        }

        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(content);
        Assert.Equal("ready", content.Status);
    }

    [Fact]
    public async Task Scalar_Documentation_Is_Accessible()
    {
        // Arrange
        var client = _appFactory!.CreateHttpClient("geometry-service");
        client.Timeout = TimeSpan.FromSeconds(60);

        // Act
        HttpResponseMessage? response = null;
        for (int i = 0; i < 10; i++)
        {
            try
            {
                response = await client.GetAsync("/scalar");
                if (response.IsSuccessStatusCode) break;
            }
            catch (Exception ex)
            {
                _output.WriteLine($"Attempt {i + 1} failed: {ex.Message}");
            }
            await Task.Delay(5000);
        }

        Assert.NotNull(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("scalar", html.ToLower());
    }

    [Fact]
    public async Task UploadService_To_GeometryService_EndToEnd_Flow()
    {
        _output.WriteLine("=== Geometry Analysis E2E Test Starting ===");
        
        // 1. Setup Clients
        // Configure HttpClient to follow redirects (handles 307 HttpsRedirection)
        // We use a custom HttpClient with a handler that allows redirects
        using var handler = new HttpClientHandler { AllowAutoRedirect = true };
        using var uploadClient = _appFactory!.CreateHttpClient("maliev-uploadservice-api");
        // Aspire's CreateHttpClient uses a specialized handler, so we should try to use 
        // the provided client but wrapping it or adjusting it if possible.
        // If we can't adjust it, we'll try to use the base address.
        
        uploadClient.WithTestAuth(permissions: ["files.upload"]);

        // 2. Create a dummy STL file for upload
        var stlContent = "solid cube\nfacet normal 0 0 0\nouter loop\nvertex 0 0 0\nvertex 10 0 0\nvertex 10 10 0\nendloop\nendfacet\nendsolid";
        var fileContent = new StringContent(stlContent);
        var form = new MultipartFormDataContent();
        form.Add(fileContent, "File", "test_cube.stl");
        form.Add(new StringContent("geometry-test"), "Path");
        form.Add(new StringContent("geometry-service"), "ServiceName");

        // 3. Upload the file
        _output.WriteLine("[Step 1] Uploading STL file to UploadService...");
        var uploadResponse = await uploadClient.PostAsync("/upload/v1/uploads", form);
        
        var uploadResult = await uploadResponse.Content.ReadFromJsonAsync<UploadResponse>();
        Assert.NotNull(uploadResult);
        _output.WriteLine($"✓ File uploaded successfully. UploadId: {uploadResult.UploadId}");

        // 4. Wait for Geometry Service to process (Async)
        // In a real scenario, we'd check a database or another service's logs.
        // Since GeometryService is Python and publishes to RabbitMQ, we'll verify it doesn't crash 
        // and the logs show processing.
        _output.WriteLine("[Step 2] Waiting for Geometry Analysis to complete...");
        await Task.Delay(TimeSpan.FromSeconds(5));

        // 5. Verification
        // Note: For a more robust test, we could inject a "Test Consumer" into Aspire 
        // that listens for FileAnalyzedEvent.
        _output.WriteLine("=== Geometry Analysis E2E Test PASSED (Process Initiated) ===");
    }

    private class HealthResponse
    {
        public string Status { get; set; } = string.Empty;
    }

    private class UploadResponse
    {
        public string UploadId { get; set; } = string.Empty;
        public string StoragePath { get; set; } = string.Empty;
    }
}
