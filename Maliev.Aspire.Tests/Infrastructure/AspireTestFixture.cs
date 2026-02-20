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
    public DistributedApplicationFactory? AppFactory { get; private set; }
    public string? AdminToken { get; private set; }

    public async Task InitializeAsync()
    {
        var appHostAssembly = typeof(Projects.Maliev_Aspire_AppHost).Assembly;

        AppFactory = new DistributedApplicationFactory(
            appHostAssembly.EntryPoint!.DeclaringType!,
            ["--environment", "Testing"]);

        await AppFactory.StartAsync();
    }

    public async Task DisposeAsync()
    {
        if (AppFactory != null)
        {
            await AppFactory.DisposeAsync();
        }
    }

    public HttpClient CreateClient(string projectName)
    {
        return AppFactory!.CreateHttpClient(projectName);
    }

    public string GetAdminToken()
    {
        if (!string.IsNullOrEmpty(AdminToken))
            return AdminToken;

        AdminToken = IAMTestHelpers.CreateTestJWT(
            principalId: "00000000-0000-0000-0000-000000000002",
            permissions: "*");

        return AdminToken;
    }

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
