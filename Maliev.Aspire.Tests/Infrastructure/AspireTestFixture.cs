using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using Aspire.Hosting.Testing;
using Maliev.Aspire.ServiceDefaults.Testing;
using Xunit;

namespace Maliev.Aspire.Tests.Infrastructure;

/// <summary>
/// Shared fixture for all domain workflow tests. Starts the Aspire infrastructure once
/// and shares it across all tests in the collection, dramatically improving test performance.
/// </summary>
public class AspireTestFixture : IAsyncLifetime
{
    private readonly ConcurrentDictionary<string, Uri> _httpsBaseAddresses = new();

    /// <summary>
    /// The distributed application factory for creating test clients.
    /// </summary>
    public DistributedApplicationFactory? AppFactory { get; private set; }

    /// <summary>
    /// The admin JWT token for authenticated requests.
    /// </summary>
    public string? AdminToken { get; private set; }

    /// <summary>
    /// How long the AppHost took to start, for diagnostics.
    /// </summary>
    public TimeSpan StartupDuration { get; private set; }

    /// <summary>
    /// Initializes the test fixture by starting the Aspire application.
    /// </summary>
    /// <returns>A task representing the asynchronous initialization.</returns>
    public async Task InitializeAsync()
    {
        Console.WriteLine("[AspireTestFixture] Starting AppHost in Testing environment...");
        var sw = Stopwatch.StartNew();

        Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Testing");

        var appHostAssembly = typeof(Projects.Maliev_Aspire_AppHost).Assembly;

        AppFactory = new DistributedApplicationFactory(
            appHostAssembly.EntryPoint!.DeclaringType!,
            ["--environment", "Testing"]);

        await AppFactory.StartAsync();

        sw.Stop();
        StartupDuration = sw.Elapsed;
        Console.WriteLine($"[AspireTestFixture] AppHost started in {sw.Elapsed.TotalSeconds:F1}s");
    }

    /// <summary>
    /// Disposes of the test fixture by stopping the Aspire application.
    /// </summary>
    /// <returns>A task representing the asynchronous disposal.</returns>
    public async Task DisposeAsync()
    {
        if (AppFactory != null)
        {
            await AppFactory.DisposeAsync();
        }
    }

    /// <summary>
    /// Creates an authenticated HTTP client for the specified project using the admin token.
    /// Automatically resolves the HTTPS base address to avoid auth header stripping during redirects.
    /// </summary>
    /// <param name="projectName">The name of the project to create a client for.</param>
    /// <returns>An authenticated HTTP client for making requests to the project.</returns>
    public HttpClient CreateAuthenticatedClient(string projectName)
    {
        var token = GetAdminToken();
        var baseAddress = ResolveHttpsBaseAddress(projectName);

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = delegate { return true; }
        };

        var client = new HttpClient(handler)
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(100)
        };

        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    /// <summary>
    /// Creates an unauthenticated HTTP client for the specified project.
    /// Automatically resolves the HTTPS base address to avoid redirect issues.
    /// </summary>
    /// <param name="projectName">The name of the project to create a client for.</param>
    /// <returns>An HTTP client for making unauthenticated requests to the project.</returns>
    public HttpClient CreateClient(string projectName)
    {
        var baseAddress = ResolveHttpsBaseAddress(projectName);

        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = delegate { return true; }
        };

        var client = new HttpClient(handler)
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(100)
        };

        return client;
    }

    /// <summary>
    /// Resolves the HTTPS base address for a project by probing the HTTP endpoint.
    /// Services use <c>UseHttpsRedirection()</c> which causes a 308 redirect from HTTP to HTTPS.
    /// The default <see cref="HttpClientHandler"/> strips Authorization headers on redirects,
    /// so we probe once to discover the HTTPS URL and use it directly for all subsequent requests.
    /// Results are cached per project name.
    /// </summary>
    /// <param name="projectName">The Aspire resource name of the project.</param>
    /// <returns>The HTTPS base address URI for the project.</returns>
    private Uri ResolveHttpsBaseAddress(string projectName)
    {
        return _httpsBaseAddresses.GetOrAdd(projectName, ResolveHttpsBaseAddressCore);
    }

    /// <summary>
    /// Core logic to discover the HTTPS base address by probing the HTTP endpoint.
    /// </summary>
    /// <param name="projectName">The Aspire resource name of the project.</param>
    /// <returns>The resolved base address URI.</returns>
    private Uri ResolveHttpsBaseAddressCore(string projectName)
    {
        var baseClient = AppFactory!.CreateHttpClient(projectName);
        var httpBase = baseClient.BaseAddress!;

        if (httpBase.Scheme == "https")
        {
            baseClient.Dispose();
            return httpBase;
        }

        using var probeHandler = new HttpClientHandler
        {
            AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = delegate { return true; }
        };

        using var probeClient = new HttpClient(probeHandler)
        {
            BaseAddress = httpBase,
            Timeout = TimeSpan.FromSeconds(10)
        };

        try
        {
            var response = probeClient.GetAsync("/").GetAwaiter().GetResult();

            if ((int)response.StatusCode >= 300 && (int)response.StatusCode <= 399)
            {
                var location = response.Headers.Location;
                if (location != null)
                {
                    var httpsBase = location.IsAbsoluteUri
                        ? new Uri($"{location.Scheme}://{location.Authority}/")
                        : new Uri($"https://{httpBase.Host}:{location.Port}/");

                    Console.WriteLine($"[AspireTestFixture] {projectName}: HTTP {httpBase} -> HTTPS {httpsBase}");
                    baseClient.Dispose();
                    return httpsBase;
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AspireTestFixture] HTTPS probe failed for {projectName}: {ex.Message}");
        }

        Console.WriteLine($"[AspireTestFixture] {projectName}: No redirect detected, using HTTP {httpBase}");
        baseClient.Dispose();
        return httpBase;
    }

    /// <summary>
    /// Gets the admin JWT token for authenticated requests, creating one if it doesn't exist.
    /// </summary>
    /// <returns>The admin JWT token string.</returns>
    public string GetAdminToken()
    {
        if (!string.IsNullOrEmpty(AdminToken))
        {
            return AdminToken;
        }

        AdminToken = IAMTestHelpers.CreateTestJWT(
            principalId: "00000000-0000-0000-0000-000000000002",
            permissions: "*");

        return AdminToken;
    }
}

/// <summary>
/// Collection definition for all domain workflow tests that need the Aspire infrastructure.
/// All tests in this collection share the same AspireTestFixture instance.
/// </summary>
[CollectionDefinition("AspireDomainTests")]
public class AspireDomainTestCollection : ICollectionFixture<AspireTestFixture>
{
}
