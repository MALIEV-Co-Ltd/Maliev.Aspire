using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
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
    /// ContactService must authenticate only with its own secret, receive bounded global authority,
    /// call CountryService, and preserve anonymous contact intake.
    /// </summary>
    [Fact]
    public async Task ContactServiceCredential_HostedFlow_IsolatedAndBounded()
    {
        var authSecret = await fixture.GetSecretParameterValueAsync("AuthServiceLocalClientSecret");
        var contactSecret = await fixture.GetSecretParameterValueAsync("ContactServiceLocalClientSecret");
        using var authClient = await fixture.CreateLiveAuthClientAsync();

        using var authWithContactSecret = await authClient.PostAsJsonAsync("/auth/v1/service/login", new
        {
            client_id = "service-auth-service",
            client_secret = contactSecret
        });
        using var contactWithAuthSecret = await authClient.PostAsJsonAsync("/auth/v1/service/login", new
        {
            client_id = "service-contact-service",
            client_secret = authSecret
        });
        Assert.Equal(HttpStatusCode.Unauthorized, authWithContactSecret.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, contactWithAuthSecret.StatusCode);

        using var login = await authClient.PostAsJsonAsync("/auth/v1/service/login", new
        {
            client_id = "service-contact-service",
            client_secret = contactSecret
        });
        var loginBody = await login.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var loginJson = JsonDocument.Parse(loginBody);
        var accessToken = loginJson.RootElement.GetProperty("access_token").GetString();
        var token = new JwtSecurityTokenHandler().ReadJwtToken(accessToken);
        Assert.Equal("ContactService", token.Claims.Single(claim => claim.Type == "service_name").Value);
        Assert.Equal(
            ["country.countries.read"],
            token.Claims.Where(claim => claim.Type == "permissions").Select(claim => claim.Value));
        Assert.Equal(
            ["roles.workloads.contact-service.v1"],
            token.Claims.Where(claim => claim.Type == "roles").Select(claim => claim.Value));
        Assert.DoesNotContain(token.Claims, claim => claim.Value.Contains("upload.files", StringComparison.Ordinal));

        using var countryClient = await fixture.CreateLiveCountryClientAsync();
        countryClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var thailand = await countryClient.GetAsync("/country/v1/countries/iso2/TH");
        Assert.Equal(HttpStatusCode.OK, thailand.StatusCode);
        var thailandJson = await thailand.Content.ReadFromJsonAsync<JsonElement>();
        var thailandId = thailandJson.GetProperty("id").GetGuid();
        using var forbiddenCreate = await countryClient.PostAsJsonAsync("/country/v1/admin/countries", new { });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenCreate.StatusCode);

        using var contactClient = await fixture.CreateLiveContactClientAsync();
        using var createContact = await contactClient.PostAsJsonAsync("/contact/v1/contacts", new
        {
            FullName = "Hosted Contact Test",
            Email = $"hosted.{Guid.NewGuid():N}@example.com",
            Subject = "Hosted identity boundary",
            Message = "Validates the ContactService local workload credential exchange.",
            CountryId = thailandId,
            ContactType = 0
        });
        var contactBody = await createContact.Content.ReadAsStringAsync();
        output.WriteLine($"Hosted ContactService submission returned {(int)createContact.StatusCode}.");
        Assert.True(
            createContact.StatusCode == HttpStatusCode.Created,
            $"Expected 201 Created but received {(int)createContact.StatusCode}: {contactBody}");
    }

    /// <summary>
    /// SearchService credentials must be isolated from every other local workload and issue only
    /// the canonical live-permission-check authority before Search runtime wiring is enabled.
    /// </summary>
    [Fact]
    public async Task SearchServiceCredential_HostedAuthIamBoundary_IsolatedAndBounded()
    {
        var authSecret = await fixture.GetSecretParameterValueAsync("AuthServiceLocalClientSecret");
        var contactSecret = await fixture.GetSecretParameterValueAsync("ContactServiceLocalClientSecret");
        var searchSecret = await fixture.GetSecretParameterValueAsync("SearchServiceLocalClientSecret");
        using var authClient = await fixture.CreateLiveAuthClientAsync();

        var credentials = new[]
        {
            (ClientId: "service-auth-service", Secret: authSecret),
            (ClientId: "service-contact-service", Secret: contactSecret),
            (ClientId: "service-search-service", Secret: searchSecret)
        };
        foreach (var credential in credentials)
        {
            foreach (var crossedSecret in credentials
                         .Where(candidate => candidate.ClientId != credential.ClientId)
                         .Select(candidate => candidate.Secret))
            {
                using var rejected = await authClient.PostAsJsonAsync("/auth/v1/service/login", new
                {
                    client_id = credential.ClientId,
                    client_secret = crossedSecret
                });
                Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
            }
        }

        using var login = await authClient.PostAsJsonAsync("/auth/v1/service/login", new
        {
            client_id = "service-search-service",
            client_secret = searchSecret
        });
        var loginBody = await login.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var loginJson = JsonDocument.Parse(loginBody);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(
            loginJson.RootElement.GetProperty("access_token").GetString());

        Assert.Equal("service-search-service", token.Claims.Single(claim => claim.Type == "client_id").Value);
        Assert.Equal("SearchService", token.Claims.Single(claim => claim.Type == "service_name").Value);
        Assert.Equal(
            ["iam.auth.check-permission"],
            token.Claims.Where(claim => claim.Type == "permissions").Select(claim => claim.Value));
        Assert.Equal(
            ["roles.workloads.search-service.v1"],
            token.Claims.Where(claim => claim.Type == "roles").Select(claim => claim.Value));
        Assert.DoesNotContain(token.Claims, claim => claim.Value == "*");
        Assert.DoesNotContain(token.Claims, claim =>
            claim.Value.Contains("platform.owner", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// RegistryService credentials must remain distinct from every other local workload and issue only
    /// the canonical live-permission-check authority before Registry runtime wiring is enabled.
    /// </summary>
    [Fact]
    public async Task RegistryServiceCredential_HostedAuthIamBoundary_IsolatedAndBounded()
    {
        var authSecret = await fixture.GetSecretParameterValueAsync("AuthServiceLocalClientSecret");
        var contactSecret = await fixture.GetSecretParameterValueAsync("ContactServiceLocalClientSecret");
        var searchSecret = await fixture.GetSecretParameterValueAsync("SearchServiceLocalClientSecret");
        var registrySecret = await fixture.GetSecretParameterValueAsync("RegistryServiceLocalClientSecret");
        using var authClient = await fixture.CreateLiveAuthClientAsync();

        using var registryLogin = await authClient.PostAsJsonAsync("/auth/v1/service/login", new
        {
            client_id = "service-registry-service",
            client_secret = registrySecret
        });
        var loginBody = await registryLogin.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, registryLogin.StatusCode);
        using var loginJson = JsonDocument.Parse(loginBody);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(
            loginJson.RootElement.GetProperty("access_token").GetString());

        Assert.Equal("service-registry-service", token.Claims.Single(claim => claim.Type == "client_id").Value);
        Assert.Equal("RegistryService", token.Claims.Single(claim => claim.Type == "service_name").Value);
        Assert.Equal(
            ["iam.auth.check-permission"],
            token.Claims.Where(claim => claim.Type == "permissions").Select(claim => claim.Value));
        Assert.Equal(
            ["roles.workloads.registry-service.v1"],
            token.Claims.Where(claim => claim.Type == "roles").Select(claim => claim.Value));
        Assert.DoesNotContain(token.Claims, claim => claim.Value == "*");
        Assert.DoesNotContain(token.Claims, claim =>
            claim.Value.Contains("platform.owner", StringComparison.OrdinalIgnoreCase));

        foreach (var crossedCredential in new[]
        {
            (ClientId: "service-auth-service", Secret: registrySecret),
            (ClientId: "service-contact-service", Secret: registrySecret),
            (ClientId: "service-search-service", Secret: registrySecret),
            (ClientId: "service-registry-service", Secret: authSecret)
        })
        {
            using var rejected = await authClient.PostAsJsonAsync("/auth/v1/service/login", new
            {
                client_id = crossedCredential.ClientId,
                client_secret = crossedCredential.Secret
            });
            Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        }

        Assert.NotEqual(contactSecret, registrySecret);
        Assert.NotEqual(searchSecret, registrySecret);
    }

    /// <summary>
    /// CountryService credentials must be unique across all five local workloads and issue only
    /// the canonical live-permission-check authority before Country runtime wiring is enabled.
    /// </summary>
    [Fact]
    public async Task CountryServiceCredential_HostedAuthIamBoundary_AllFiveIdentitiesAreIsolatedAndBounded()
    {
        var credentials = new[]
        {
            (ClientId: "service-auth-service", Secret: await fixture.GetSecretParameterValueAsync("AuthServiceLocalClientSecret")),
            (ClientId: "service-contact-service", Secret: await fixture.GetSecretParameterValueAsync("ContactServiceLocalClientSecret")),
            (ClientId: "service-search-service", Secret: await fixture.GetSecretParameterValueAsync("SearchServiceLocalClientSecret")),
            (ClientId: "service-registry-service", Secret: await fixture.GetSecretParameterValueAsync("RegistryServiceLocalClientSecret")),
            (ClientId: "service-country-service", Secret: await fixture.GetSecretParameterValueAsync("CountryServiceLocalClientSecret"))
        };
        Assert.Equal(5, credentials.Select(credential => credential.Secret).Distinct().Count());
        using var authClient = await fixture.CreateLiveAuthClientAsync();

        for (var index = 0; index < credentials.Length; index++)
        {
            var credential = credentials[index];
            var crossedSecret = credentials[(index + 1) % credentials.Length].Secret;
            using var rejected = await authClient.PostAsJsonAsync("/auth/v1/service/login", new
            {
                client_id = credential.ClientId,
                client_secret = crossedSecret
            });
            Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        }

        using var countryLogin = await authClient.PostAsJsonAsync("/auth/v1/service/login", new
        {
            client_id = "service-country-service",
            client_secret = credentials[^1].Secret
        });
        var loginBody = await countryLogin.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, countryLogin.StatusCode);
        using var loginJson = JsonDocument.Parse(loginBody);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(
            loginJson.RootElement.GetProperty("access_token").GetString());

        Assert.Equal("service-country-service", token.Claims.Single(claim => claim.Type == "client_id").Value);
        Assert.Equal("CountryService", token.Claims.Single(claim => claim.Type == "service_name").Value);
        Assert.Equal(
            ["iam.auth.check-permission"],
            token.Claims.Where(claim => claim.Type == "permissions").Select(claim => claim.Value));
        Assert.Equal(
            ["roles.workloads.country-service.v1"],
            token.Claims.Where(claim => claim.Type == "roles").Select(claim => claim.Value));
        Assert.DoesNotContain(token.Claims, claim => claim.Value == "*");
        Assert.DoesNotContain(token.Claims, claim =>
            claim.Value.Contains("platform.owner", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// CurrencyService credentials must be unique across all six local workloads, reject every
    /// ordered crossed-secret pair, and issue only the canonical live-permission-check authority.
    /// </summary>
    [Fact]
    public async Task CurrencyServiceCredential_HostedAuthIamBoundary_AllSixIdentitiesAreIsolatedAndBounded()
    {
        var credentials = new[]
        {
            (ClientId: "service-auth-service", Secret: await fixture.GetSecretParameterValueAsync("AuthServiceLocalClientSecret")),
            (ClientId: "service-contact-service", Secret: await fixture.GetSecretParameterValueAsync("ContactServiceLocalClientSecret")),
            (ClientId: "service-search-service", Secret: await fixture.GetSecretParameterValueAsync("SearchServiceLocalClientSecret")),
            (ClientId: "service-registry-service", Secret: await fixture.GetSecretParameterValueAsync("RegistryServiceLocalClientSecret")),
            (ClientId: "service-country-service", Secret: await fixture.GetSecretParameterValueAsync("CountryServiceLocalClientSecret")),
            (ClientId: "service-currency-service", Secret: await fixture.GetSecretParameterValueAsync("CurrencyServiceLocalClientSecret"))
        };
        Assert.Equal(6, credentials.Select(credential => credential.Secret).Distinct().Count());
        using var authClient = await fixture.CreateLiveAuthClientAsync();

        foreach (var credential in credentials)
        {
            foreach (var crossedSecret in credentials
                         .Where(candidate => candidate.ClientId != credential.ClientId)
                         .Select(candidate => candidate.Secret))
            {
                using var rejected = await authClient.PostAsJsonAsync("/auth/v1/service/login", new
                {
                    client_id = credential.ClientId,
                    client_secret = crossedSecret
                });
                Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
            }
        }

        using var currencyLogin = await authClient.PostAsJsonAsync("/auth/v1/service/login", new
        {
            client_id = "service-currency-service",
            client_secret = credentials[^1].Secret
        });
        var loginBody = await currencyLogin.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, currencyLogin.StatusCode);
        using var loginJson = JsonDocument.Parse(loginBody);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(
            loginJson.RootElement.GetProperty("access_token").GetString());

        Assert.Equal("service-currency-service", token.Claims.Single(claim => claim.Type == "client_id").Value);
        Assert.Equal("CurrencyService", token.Claims.Single(claim => claim.Type == "service_name").Value);
        Assert.Equal(
            ["iam.auth.check-permission"],
            token.Claims.Where(claim => claim.Type == "permissions").Select(claim => claim.Value));
        Assert.Equal(
            ["roles.workloads.currency-service.v1"],
            token.Claims.Where(claim => claim.Type == "roles").Select(claim => claim.Value));
        Assert.DoesNotContain(token.Claims, claim => claim.Value == "*");
        Assert.DoesNotContain(token.Claims, claim =>
            claim.Value.Contains("platform.owner", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// AccountingService credentials must be independent from every preceding local workload and
    /// issue only the canonical live-permission-check authority.
    /// </summary>
    [Fact]
    public async Task AccountingServiceCredential_HostedAuthIamBoundary_SevenIdentitiesAreIsolatedAndBounded()
    {
        var credentials = new[]
        {
            (ClientId: "service-auth-service", Secret: await fixture.GetSecretParameterValueAsync("AuthServiceLocalClientSecret")),
            (ClientId: "service-contact-service", Secret: await fixture.GetSecretParameterValueAsync("ContactServiceLocalClientSecret")),
            (ClientId: "service-search-service", Secret: await fixture.GetSecretParameterValueAsync("SearchServiceLocalClientSecret")),
            (ClientId: "service-registry-service", Secret: await fixture.GetSecretParameterValueAsync("RegistryServiceLocalClientSecret")),
            (ClientId: "service-country-service", Secret: await fixture.GetSecretParameterValueAsync("CountryServiceLocalClientSecret")),
            (ClientId: "service-currency-service", Secret: await fixture.GetSecretParameterValueAsync("CurrencyServiceLocalClientSecret")),
            (ClientId: "service-accounting-service", Secret: await fixture.GetSecretParameterValueAsync("AccountingServiceLocalClientSecret"))
        };
        Assert.Equal(7, credentials.Select(credential => credential.Secret).Distinct().Count());
        using var authClient = await fixture.CreateLiveAuthClientAsync();

        using var accountingLogin = await authClient.PostAsJsonAsync("/auth/v1/service/login", new
        {
            client_id = "service-accounting-service",
            client_secret = credentials[^1].Secret
        });
        var loginBody = await accountingLogin.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, accountingLogin.StatusCode);
        using var loginJson = JsonDocument.Parse(loginBody);
        var token = new JwtSecurityTokenHandler().ReadJwtToken(
            loginJson.RootElement.GetProperty("access_token").GetString());

        Assert.Equal("service-accounting-service", token.Claims.Single(claim => claim.Type == "client_id").Value);
        Assert.Equal("AccountingService", token.Claims.Single(claim => claim.Type == "service_name").Value);
        Assert.Equal(
            ["iam.auth.check-permission"],
            token.Claims.Where(claim => claim.Type == "permissions").Select(claim => claim.Value));
        Assert.Equal(
            ["roles.workloads.accounting-service.v1"],
            token.Claims.Where(claim => claim.Type == "roles").Select(claim => claim.Value));
        Assert.DoesNotContain(token.Claims, claim => claim.Value == "*");
        Assert.DoesNotContain(token.Claims, claim =>
            claim.Value.Contains("platform.owner", StringComparison.OrdinalIgnoreCase));

        var accountingCredential = credentials[^1];
        foreach (var precedingCredential in credentials[..^1])
        {
            using var precedingClientWithAccountingSecret = await authClient.PostAsJsonAsync(
                "/auth/v1/service/login",
                new
                {
                    client_id = precedingCredential.ClientId,
                    client_secret = accountingCredential.Secret
                });
            Assert.Equal(HttpStatusCode.Unauthorized, precedingClientWithAccountingSecret.StatusCode);

            using var accountingClientWithPrecedingSecret = await authClient.PostAsJsonAsync(
                "/auth/v1/service/login",
                new
                {
                    client_id = accountingCredential.ClientId,
                    client_secret = precedingCredential.Secret
                });
            Assert.Equal(HttpStatusCode.Unauthorized, accountingClientWithPrecedingSecret.StatusCode);
        }
    }

    /// <summary>
    /// The evaluated bounded model must isolate capability material and keep live-check credentials independent.
    /// </summary>
    [Fact]
    public async Task BoundedModel_EvaluatedEnvironment_IsolatesCapabilityMaterialAndLiveCheckVerifier()
    {
        var model = fixture.GetModel();
        var authSecret = await fixture.GetSecretParameterValueAsync("AuthServiceLocalClientSecret");
        var contactSecret = await fixture.GetSecretParameterValueAsync("ContactServiceLocalClientSecret");
        var searchSecret = await fixture.GetSecretParameterValueAsync("SearchServiceLocalClientSecret");
        var registrySecret = await fixture.GetSecretParameterValueAsync("RegistryServiceLocalClientSecret");
        var countrySecret = await fixture.GetSecretParameterValueAsync("CountryServiceLocalClientSecret");
        var currencySecret = await fixture.GetSecretParameterValueAsync("CurrencyServiceLocalClientSecret");
        var accountingSecret = await fixture.GetSecretParameterValueAsync("AccountingServiceLocalClientSecret");
        Assert.DoesNotContain(
            model.Resources,
            resource => string.Equals(resource.Name, "SearchService", StringComparison.Ordinal));
        Assert.DoesNotContain(
            model.Resources,
            resource => string.Equals(resource.Name, "RegistryService", StringComparison.Ordinal));
        Assert.DoesNotContain(
            model.Resources,
            resource => string.Equals(resource.Name, "CurrencyService", StringComparison.Ordinal));
        Assert.DoesNotContain(
            model.Resources,
            resource => string.Equals(resource.Name, "AccountingService", StringComparison.Ordinal));
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
            else if (resource.Name == "ContactService")
            {
                Assert.Empty(capabilityKeys);
                Assert.Equal("service-contact-service", environment["ServiceAuthentication__ClientId"]);
                Assert.Equal(contactSecret, environment["ServiceAuthentication__ClientSecret"]);
                Assert.DoesNotContain("Jwt__PrivateKey", environment.Keys);
                Assert.DoesNotContain("Jwt__SecurityKey", environment.Keys);
                Assert.Contains("Jwt__PublicKey", environment.Keys);
                Assert.Contains("Jwt__Issuer", environment.Keys);
                Assert.Contains("Jwt__Audience", environment.Keys);
            }
            else
            {
                Assert.Empty(capabilityKeys);
            }

            if (resource.Name != "AuthService")
            {
                Assert.DoesNotContain(environment, pair => Equals(pair.Value, authSecret));
            }

            if (resource.Name != "ContactService")
            {
                Assert.DoesNotContain(environment, pair => Equals(pair.Value, contactSecret));
            }

            // Search runtime is intentionally absent until its central-exchange wiring lands.
            // Its raw credential must therefore remain unprojected to every process resource.
            Assert.DoesNotContain(environment, pair => Equals(pair.Value, searchSecret));

            // Registry runtime is intentionally absent until its central-exchange wiring lands.
            // Its raw credential must therefore remain unprojected to every process resource.
            Assert.DoesNotContain(environment, pair => Equals(pair.Value, registrySecret));

            // Country's central-exchange runtime wiring has not landed yet.
            // Its raw credential must therefore remain unprojected to every process resource.
            Assert.DoesNotContain(environment, pair => Equals(pair.Value, countrySecret));

            // This bounded capability host intentionally excludes Currency runtime wiring.
            // Its raw credential must therefore remain unprojected to every process resource here.
            Assert.DoesNotContain(environment, pair => Equals(pair.Value, currencySecret));

            // This bounded capability host intentionally excludes Accounting runtime wiring.
            // Its raw credential must therefore remain unprojected to every process resource here.
            Assert.DoesNotContain(environment, pair => Equals(pair.Value, accountingSecret));
        }

        Assert.NotNull(iamVerifier);
        Assert.NotEqual(authDerivedVerifier, iamVerifier);
        Assert.Equal($"{IamPublicKeyPrefix}{authActiveKeyId}", iamPublicKeyName);
    }
}

/// <summary>Starts the bounded local identity vertical slice and its seeders.</summary>
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
        => await CreateLiveClientAsync(
            "AuthService",
            "/auth/aspire-liveness",
            "Hosted AuthService did not become live for the capability system test.");

    /// <summary>Creates a CountryService client after liveness succeeds.</summary>
    public async Task<HttpClient> CreateLiveCountryClientAsync()
        => await CreateLiveClientAsync(
            "CountryService",
            "/country/aspire-liveness",
            "Hosted CountryService did not become live for the capability system test.");

    /// <summary>Creates a ContactService client after liveness succeeds.</summary>
    public async Task<HttpClient> CreateLiveContactClientAsync()
        => await CreateLiveClientAsync(
            "ContactService",
            "/contact/aspire-liveness",
            "Hosted ContactService did not become live for the capability system test.");

    private async Task<HttpClient> CreateLiveClientAsync(
        string resourceName,
        string livenessPath,
        string failureMessage)
    {
        var handler = new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = delegate { return true; }
        };
        var client = new HttpClient(handler)
        {
            BaseAddress = _factory!.GetEndpoint(resourceName, "https"),
            Timeout = TimeSpan.FromSeconds(100)
        };
        using var liveness = await TestHelpers.WaitForSuccessAsync(
            () => client.GetAsync(livenessPath),
            timeout: TimeSpan.FromMinutes(3),
            message: failureMessage);
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
