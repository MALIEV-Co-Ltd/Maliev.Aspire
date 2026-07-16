using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Maliev.Aspire.Tests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;

namespace Maliev.Aspire.Tests.Domain.Foundation;

/// <summary>Real hosted Auth-to-IAM token-issuance capability workflow tests.</summary>
[Collection("TokenIssuanceCapabilitySystemTests")]
public sealed class TokenIssuanceCapabilitySystemTests(
    TokenIssuanceCapabilitySystemFixture fixture,
    ITestOutputHelper output)
{
    private const string AuthActiveKeyId = "Auth__TokenIssuanceCapability__ActiveKeyId";
    private const string AuthPrivateKey = "Auth__TokenIssuanceCapability__PrivateKey";
    private const string IamPublicKeyPrefix = "IAM__TokenIssuanceCapability__PublicKeys__";
    private const string IamLiveCheckRawCredential = "IAM__LivePermissionChecks__Credential";
    private const string IamLiveCheckVerifier = "IAM__LivePermissionChecks__CredentialHashes__IntranetBff";

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

    /// <summary>
    /// The evaluated bounded model must isolate capability material and keep live-check credentials independent.
    /// </summary>
    [Fact]
    public async Task BoundedModel_EvaluatedEnvironment_IsolatesCapabilityMaterialAndLiveCheckVerifier()
    {
        var model = fixture.GetModel();
        var authSecret = await fixture.GetSecretParameterValueAsync("AuthServiceLocalClientSecret");
        var authSecretBytes = Encoding.UTF8.GetBytes(authSecret);
        var authDerivedVerifierDigest = SHA256.HashData(authSecretBytes);
        var authDerivedVerifier = Convert.ToBase64String(authDerivedVerifierDigest);
        CryptographicOperations.ZeroMemory(authSecretBytes);
        CryptographicOperations.ZeroMemory(authDerivedVerifierDigest);
        string? authActiveKeyId = null;
        string? iamPublicKeyName = null;
        string? iamVerifier = null;

        foreach (var resource in model.Resources.OfType<IResourceWithEnvironment>())
        {
            var configuration = await ExecutionConfigurationBuilder
                .Create(resource)
                .WithEnvironmentVariablesConfig()
                .BuildAsync(
                    new DistributedApplicationExecutionContext(DistributedApplicationOperation.Run),
                    NullLogger.Instance,
                    CancellationToken.None);
            var environment = configuration.EnvironmentVariables
                .ToDictionary(StringComparer.Ordinal);
            Assert.DoesNotContain(IamLiveCheckRawCredential, environment.Keys);
            var capabilityKeys = environment.Keys
                .Where(key =>
                    key is AuthActiveKeyId or AuthPrivateKey ||
                    key.StartsWith(IamPublicKeyPrefix, StringComparison.Ordinal))
                .Order(StringComparer.Ordinal)
                .ToArray();

            if (resource.Name == "AuthService")
            {
                Assert.Equal([AuthActiveKeyId, AuthPrivateKey], capabilityKeys);
                authActiveKeyId = Assert.IsType<string>(environment[AuthActiveKeyId]);
                Assert.Contains(
                    "BEGIN PRIVATE KEY",
                    Assert.IsType<string>(environment[AuthPrivateKey]),
                    StringComparison.Ordinal);
                Assert.DoesNotContain(environment.Keys, key =>
                    key.StartsWith(IamPublicKeyPrefix, StringComparison.Ordinal));
            }
            else if (resource.Name == "IAMService")
            {
                var publicKey = Assert.Single(capabilityKeys);
                Assert.StartsWith(IamPublicKeyPrefix, publicKey, StringComparison.Ordinal);
                iamPublicKeyName = publicKey;
                Assert.False(string.IsNullOrWhiteSpace(
                    Assert.IsType<string>(environment[publicKey])));
                Assert.DoesNotContain(AuthActiveKeyId, environment.Keys);
                Assert.DoesNotContain(AuthPrivateKey, environment.Keys);
                iamVerifier = Assert.IsType<string>(environment[IamLiveCheckVerifier]);
            }
            else
            {
                Assert.Empty(capabilityKeys);
            }
        }

        Assert.NotNull(iamVerifier);
        Assert.NotEqual(authDerivedVerifier, iamVerifier);
        Assert.Equal($"{IamPublicKeyPrefix}{authActiveKeyId}", iamPublicKeyName);
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

    /// <summary>Returns the evaluated bounded AppHost resource model.</summary>
    public DistributedApplicationModel GetModel() =>
        _factory!.Application.Services.GetRequiredService<DistributedApplicationModel>();

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
