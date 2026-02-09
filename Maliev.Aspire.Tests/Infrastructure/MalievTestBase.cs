using Aspire.Hosting.Testing;
using Maliev.Aspire.ServiceDefaults.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Infrastructure;

public abstract class MalievTestBase : IAsyncLifetime
{
    protected DistributedApplicationFactory? AppFactory;
    protected readonly ITestOutputHelper Output;
    protected string? AdminToken;

    protected MalievTestBase(ITestOutputHelper output)
    {
        Output = output;
    }

    public virtual async Task InitializeAsync()
    {
        var appHostAssembly = typeof(Projects.Maliev_Aspire_AppHost).Assembly;
        AppFactory = new DistributedApplicationFactory(appHostAssembly.EntryPoint!.DeclaringType!);
        
        // Ensure configuration is loaded correctly for tests
        // You might want to override appsettings here if needed
        
        await AppFactory.StartAsync();
        Output.WriteLine("Application Started.");
    }

    public virtual async Task DisposeAsync()
    {
        if (AppFactory != null)
        {
            await AppFactory.DisposeAsync();
            Output.WriteLine("Application Disposed.");
        }
    }

    protected HttpClient CreateClient(string projectName)
    {
        return AppFactory!.CreateHttpClient(projectName);
    }

    protected async Task<string> GetAdminTokenAsync()
    {
        if (!string.IsNullOrEmpty(AdminToken))
            return AdminToken;

        Output.WriteLine("Acquiring admin platform token...");
        var authApiClient = CreateClient("maliev-authservice-api");
        
        var googleExchangeRequest = new
        {
            email = "admin@maliev.com",
            full_name = "Bootstrap Admin",
            google_user_id = "google-admin-static-id"
        };

        var response = await authApiClient.PostAsJsonAsync("/auth/v1/exchange/google", googleExchangeRequest);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(content);
        AdminToken = doc.RootElement.GetProperty("access_token").GetString();
        
        Assert.NotNull(AdminToken);
        Output.WriteLine("✓ Admin token acquired");
        
        return AdminToken!;
    }

    protected async Task<HttpClient> CreateAuthenticatedClient(string projectName)
    {
        var token = await GetAdminTokenAsync();
        var client = CreateClient(projectName);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
