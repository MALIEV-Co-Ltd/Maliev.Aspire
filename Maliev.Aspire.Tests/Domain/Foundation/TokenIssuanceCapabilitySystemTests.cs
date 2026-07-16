using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Maliev.Aspire.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.Foundation;

/// <summary>Real hosted Auth-to-IAM token-issuance capability workflow tests.</summary>
[Collection("TokenIssuanceCapabilitySystemTests")]
public sealed class TokenIssuanceCapabilitySystemTests(
    TokenIssuanceCapabilitySystemFixture fixture,
    ITestOutputHelper output)
{
    /// <summary>
    /// The real hosted Auth service must resolve the seeded workload through IAM's additive capability route.
    /// </summary>
    [Fact]
    public async Task ServiceLogin_WithLocalWorkloadCredential_ResolvesExactIamPermissionsWithoutWildcard()
    {
        var clientSecret = await fixture.GetSecretParameterValueAsync("AuthServiceLocalClientSecret");
        using var client = await fixture.CreateLiveAuthClientAsync();

        using var response = await client.PostAsJsonAsync("/auth/v1/service/login", new
        {
            client_id = "service-auth-service",
            client_secret = clientSecret
        });

        var body = await response.Content.ReadAsStringAsync();
        output.WriteLine($"AuthService local workload login returned {(int)response.StatusCode} {response.StatusCode}.");
        if (!response.IsSuccessStatusCode)
        {
            output.WriteLine($"AuthService response body: {body}");
            await Task.Delay(TimeSpan.FromSeconds(2));
        }

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(body);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(
            json.RootElement.GetProperty("access_token").GetString());

        Assert.Equal("service-auth-service", token.Claims.Single(claim => claim.Type == "client_id").Value);
        Assert.Equal("AuthService", token.Claims.Single(claim => claim.Type == "service_name").Value);
        Assert.Equal(
            ["iam.auth.resolve-permissions"],
            token.Claims.Where(claim => claim.Type == "permissions").Select(claim => claim.Value));
        Assert.Equal(
            ["roles.workloads.auth-service.v1"],
            token.Claims.Where(claim => claim.Type == "roles").Select(claim => claim.Value));
        Assert.DoesNotContain(token.Claims, claim => claim.Value == "*");
        Assert.DoesNotContain(token.Claims, claim =>
            claim.Value.Contains("platform.owner", StringComparison.OrdinalIgnoreCase));
    }
}

/// <summary>Starts only PostgreSQL, Redis, RabbitMQ, IAM, Auth, and their local seeders.</summary>
public sealed class TokenIssuanceCapabilitySystemFixture : IAsyncLifetime
{
    private CapturingDistributedApplicationFactory? _factory;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        var appHostAssembly = typeof(Projects.Maliev_Aspire_AppHost).Assembly;
        _factory = new CapturingDistributedApplicationFactory(
            appHostAssembly.EntryPoint!.DeclaringType!,
            [
                "--environment", "Testing",
                "--TokenIssuanceCapabilitySystemTest:Enabled=true"
            ]);
        await _factory.StartAsync();
    }

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
    }

    /// <summary>Creates an Auth client after its real hosted liveness endpoint succeeds.</summary>
    public async Task<HttpClient> CreateLiveAuthClientAsync()
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = delegate { return true; }
        };
        var client = new HttpClient(handler)
        {
            BaseAddress = _factory!.GetEndpoint("AuthService", "https"),
            Timeout = TimeSpan.FromSeconds(100)
        };
        using var liveness = await TestHelpers.WaitForSuccessAsync(
            () => client.GetAsync("/auth/aspire-liveness"),
            timeout: TimeSpan.FromMinutes(3),
            message: "Hosted AuthService did not become live for the capability system test.");
        return client;
    }

    /// <summary>Reads a generated secret inside the test host without logging or distributing it.</summary>
    public async Task<string> GetSecretParameterValueAsync(
        string parameterName,
        CancellationToken cancellationToken = default)
    {
        var model = _factory!.Application.Services.GetRequiredService<DistributedApplicationModel>();
        var parameter = Assert.Single(
            model.Resources.OfType<ParameterResource>(),
            resource => string.Equals(resource.Name, parameterName, StringComparison.Ordinal));
        Assert.True(parameter.Secret);
        return await parameter.GetValueAsync(cancellationToken) ?? throw new InvalidOperationException(
            $"Aspire secret parameter '{parameterName}' has no initialized value.");
    }

    private sealed class CapturingDistributedApplicationFactory(Type entryPoint, string[] args)
        : DistributedApplicationFactory(entryPoint, args)
    {
        public DistributedApplication Application { get; private set; } = null!;

        protected override void OnBuilt(DistributedApplication application)
        {
            Application = application;
            base.OnBuilt(application);
        }
    }
}

/// <summary>Collection definition for the bounded hosted capability workflow.</summary>
[CollectionDefinition("TokenIssuanceCapabilitySystemTests")]
public sealed class TokenIssuanceCapabilitySystemTestCollection
    : ICollectionFixture<TokenIssuanceCapabilitySystemFixture>;
