using System.Diagnostics;
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
    /// Creates an HTTP client for the specified project.
    /// </summary>
    /// <param name="projectName">The name of the project to create a client for.</param>
    /// <returns>An HTTP client for making requests to the project.</returns>
    public HttpClient CreateClient(string projectName)
    {
        return AppFactory!.CreateHttpClient(projectName);
    }

    /// <summary>
    /// Gets the admin JWT token for authenticated requests, creating one if it doesn't exist.
    /// </summary>
    /// <returns>The admin JWT token string.</returns>
    public string GetAdminToken()
    {
        if (!string.IsNullOrEmpty(AdminToken))
            return AdminToken;

        AdminToken = IAMTestHelpers.CreateTestJWT(
            principalId: "00000000-0000-0000-0000-000000000002",
            permissions: "*");

        return AdminToken;
    }

    /// <summary>
    /// Creates an authenticated HTTP client for the specified project using the admin token.
    /// </summary>
    /// <param name="projectName">The name of the project to create a client for.</param>
    /// <returns>An authenticated HTTP client for making requests to the project.</returns>
    public HttpClient CreateAuthenticatedClient(string projectName)
    {
        var token = GetAdminToken();
        var client = CreateClient(projectName);
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
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
