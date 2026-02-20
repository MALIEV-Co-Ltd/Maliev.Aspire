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

        // Use a factory that forces "Testing" environment for all projects
        // This enables the JWT signature validation bypass in ServiceDefaults
        AppFactory = new DistributedApplicationFactory(appHostAssembly.EntryPoint!.DeclaringType!, ["--environment", "Testing"]);

        await AppFactory.StartAsync();
        Output.WriteLine("Application Started in Testing environment.");
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

        // Since signature validation is bypassed in "Testing" environment, 
        // we can create a token locally with whatever permissions we need.
        Output.WriteLine("Creating local admin token (Signature validation bypassed in Testing mode)...");
        AdminToken = IAMTestHelpers.CreateTestJWT(
            principalId: "00000000-0000-0000-0000-000000000002", // Seeded Admin ID
            permissions: "*"); // Wildcard access

        Output.WriteLine("✓ Local admin token created");
        return AdminToken;
    }

    protected async Task<HttpClient> CreateAuthenticatedClient(string projectName)
    {
        var token = await GetAdminTokenAsync();
        var client = CreateClient(projectName);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
        return client;
    }
}
