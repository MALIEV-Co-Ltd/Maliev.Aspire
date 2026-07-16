using System.Security.Cryptography;
using System.Text;
using Maliev.Aspire.DatabaseSeeder.Seeding.Services.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Maliev.Aspire.Tests.Unit;

/// <summary>
/// Security contract tests for the Aspire-local workload credential catalog.
/// </summary>
public sealed class LocalServiceIdentitySeedTests
{
    /// <summary>
    /// Every AppHost construction must produce a new uniformly random 256-bit Base64Url secret.
    /// </summary>
    [Fact]
    public void Create_GeneratesFreshBase64UrlEncoded256BitSecretAndUppercaseHash()
    {
        var first = LocalServiceIdentitySeedMaterial.Create();
        var second = LocalServiceIdentitySeedMaterial.Create();

        Assert.NotEqual(first.RawSecret, second.RawSecret);
        Assert.Equal(43, first.RawSecret.Length);
        Assert.All(first.RawSecret, character =>
            Assert.True(char.IsAsciiLetterOrDigit(character) || character is '-' or '_'));

        var expectedHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(first.RawSecret)));
        Assert.Equal(expectedHash, first.SecretHash);
        Assert.Matches("^[0-9A-F]{64}$", first.SecretHash);
    }

    /// <summary>
    /// Every workload receives independent material and the returned catalog cannot be mutated.
    /// </summary>
    [Fact]
    public void CreateCatalog_GeneratesDistinctMaterialForEveryImmutableProfile()
    {
        var catalog = LocalServiceIdentitySeedMaterial.CreateCatalog();

        Assert.Equal(LocalServiceIdentityProfileCatalog.All.Count, catalog.Count);
        var auth = catalog[LocalServiceIdentityProfileCatalog.AuthService.WorkloadId];
        var contact = catalog[LocalServiceIdentityProfileCatalog.ContactService.WorkloadId];
        Assert.NotEqual(auth.RawSecret, contact.RawSecret);
        Assert.NotEqual(auth.SecretHash, contact.SecretHash);
        Assert.Throws<NotSupportedException>(() =>
        {
            ((IDictionary<string, LocalServiceIdentitySeedMaterial>)catalog).Add(
                "unexpected-service",
                LocalServiceIdentitySeedMaterial.Create());
        });
    }

    /// <summary>
    /// The local workload is bound to the exact server-owned IAM profile and never wildcard authority.
    /// </summary>
    [Fact]
    public void Contract_UsesExactAuthServiceV1ProfileWithoutWildcardOrPlatformOwner()
    {
        var auth = LocalServiceIdentityProfileCatalog.AuthService;
        Assert.Equal("auth-service", auth.WorkloadId);
        Assert.Equal("service-auth-service", auth.ClientId);
        Assert.Equal("AuthService", auth.ServiceName);
        Assert.Equal(1, auth.ProfileVersion);
        Assert.Equal("roles.workloads.auth-service.v1", auth.RoleId);
        Assert.Equal("iam.workload-principals.provision", LocalServiceIdentitySeedOptions.ProvisionPermission);
        Assert.DoesNotContain('*', auth.RoleId);
        Assert.DoesNotContain("platform.owner", auth.RoleId, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ContactService uses the deterministic IAM profile and cannot gain wildcard or administrator authority.
    /// </summary>
    [Fact]
    public void Contract_UsesExactContactServiceV1ProfileWithoutWildcardOrPlatformOwner()
    {
        var contact = LocalServiceIdentityProfileCatalog.ContactService;

        Assert.Equal("contact-service", contact.WorkloadId);
        Assert.Equal("service-contact-service", contact.ClientId);
        Assert.Equal("ContactService", contact.ServiceName);
        Assert.Equal(1, contact.ProfileVersion);
        Assert.Equal("roles.workloads.contact-service.v1", contact.RoleId);
        Assert.DoesNotContain('*', contact.RoleId);
        Assert.DoesNotContain("platform.owner", contact.RoleId, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            ["auth-service", "contact-service"],
            LocalServiceIdentityProfileCatalog.All.Select(profile => profile.WorkloadId).ToArray());
    }

    /// <summary>
    /// Enabling local seeding outside Development or Testing must fail closed.
    /// </summary>
    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void FromConfiguration_EnabledOutsideLocalEnvironment_Throws(string environmentName)
    {
        var configuration = BuildConfiguration(enabled: true, new string('A', 64));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            LocalServiceIdentitySeedOptions.FromConfiguration(configuration, environmentName));

        Assert.Contains("Development or Testing", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// The seeder accepts only a SHA-256 verifier and has no plaintext-secret option.
    /// </summary>
    [Fact]
    public void Options_ExposeOnlyValidatedHashToDatabaseSeeder()
    {
        var configuration = BuildConfiguration(enabled: true, new string('A', 64));

        var options = LocalServiceIdentitySeedOptions.FromConfiguration(
            configuration,
            Environments.Development);

        Assert.True(options.Enabled);
        Assert.Equal(
            new string('A', 64),
            options.GetSecretHash(LocalServiceIdentityProfileCatalog.AuthService));
        Assert.DoesNotContain(
            typeof(LocalServiceIdentitySeedOptions).GetProperties(),
            property => property.Name.Contains("Raw", StringComparison.OrdinalIgnoreCase) ||
                property.Name.Equals("ClientSecret", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Invalid or lowercase hashes must be rejected before any database write.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("abcd")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void FromConfiguration_InvalidHash_Throws(string hash)
    {
        var configuration = BuildConfiguration(enabled: true, hash);

        Assert.Throws<InvalidOperationException>(() =>
            LocalServiceIdentitySeedOptions.FromConfiguration(configuration, "Testing"));
    }

    private static IConfiguration BuildConfiguration(bool enabled, string hash) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AspireLocalServiceIdentity:Enabled"] = enabled.ToString(),
                ["AspireLocalServiceIdentity:Profiles:auth-service:SecretHash"] = hash,
                ["AspireLocalServiceIdentity:Profiles:contact-service:SecretHash"] = hash
            })
            .Build();
}
