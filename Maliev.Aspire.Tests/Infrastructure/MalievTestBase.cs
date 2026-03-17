using Aspire.Hosting.Testing;
using Maliev.Aspire.ServiceDefaults.Testing;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Infrastructure;

/// <summary>
/// Base class for MALIEV integration tests providing shared infrastructure setup.
/// </summary>
public abstract class MalievTestBase : IAsyncLifetime
{
    /// <summary>
    /// The distributed application factory for creating test clients.
    /// </summary>
    protected DistributedApplicationFactory? AppFactory;

    /// <summary>
    /// The test output helper for writing logs during test execution.
    /// </summary>
    protected readonly ITestOutputHelper Output;

    /// <summary>
    /// The admin JWT token for authenticated requests.
    /// </summary>
    protected string? AdminToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="MalievTestBase"/> class.
    /// </summary>
    /// <param name="output">The test output helper for logging.</param>
    protected MalievTestBase(ITestOutputHelper output)
    {
        Output = output;
    }

    /// <summary>
    /// Initializes the test by starting the Aspire application in Testing environment.
    /// </summary>
    /// <returns>A task representing the asynchronous initialization.</returns>
    public virtual async Task InitializeAsync()
    {
        var appHostAssembly = typeof(Projects.Maliev_Aspire_AppHost).Assembly;

        // Use a factory that forces "Testing" environment for all projects
        // This enables the JWT signature validation bypass in ServiceDefaults
        AppFactory = new DistributedApplicationFactory(appHostAssembly.EntryPoint!.DeclaringType!, ["--environment", "Testing"]);

        await AppFactory.StartAsync();
        Output.WriteLine("Application Started in Testing environment.");
    }

    /// <summary>
    /// Disposes of the test by stopping the Aspire application.
    /// </summary>
    /// <returns>A task representing the asynchronous disposal.</returns>
    public virtual async Task DisposeAsync()
    {
        if (AppFactory != null)
        {
            await AppFactory.DisposeAsync();
            Output.WriteLine("Application Disposed.");
        }
    }

    /// <summary>
    /// Creates an HTTP client for the specified project.
    /// </summary>
    /// <param name="projectName">The name of the project to create a client for.</param>
    /// <returns>An HTTP client for making requests to the project.</returns>
    protected HttpClient CreateClient(string projectName)
    {
        return AppFactory!.CreateHttpClient(projectName);
    }

    /// <summary>
    /// Gets the admin JWT token for authenticated requests, creating one if it doesn't exist.
    /// </summary>
    /// <returns>The admin JWT token string.</returns>
    protected async Task<string> GetAdminTokenAsync()
    {
        if (!string.IsNullOrEmpty(AdminToken))
            return AdminToken;

        // Since signature validation is bypassed in "Testing" environment, 
        // we can create a token locally with whatever permissions we need.
        Output.WriteLine("Creating local admin token (Signature validation bypassed in Testing mode)...");
        AdminToken = IAMTestHelpers.CreateTestJWT(
            principalId: "00000000-0000-0000-0000-000000000002", // Seeded Admin ID
            permissions: "*"); // Wildcard access

        Output.WriteLine("✓ Local admin token created");
        return AdminToken;
    }

    /// <summary>
    /// Creates an authenticated HTTP client for the specified project using the admin token.
    /// </summary>
    /// <param name="projectName">The name of the project to create a client for.</param>
    /// <returns>An authenticated HTTP client for making requests to the project.</returns>
    protected async Task<HttpClient> CreateAuthenticatedClient(string projectName)
    {
        var token = await GetAdminTokenAsync();
        var client = CreateClient(projectName);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
