using System.Security.Cryptography;
using System.Text;
using Maliev.Aspire.DatabaseSeeder.Seeding.Services.Shared;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Maliev.Aspire.Tests.Unit;

/// <summary>
/// Security contract tests for the Aspire-local AuthService workload credential.
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
    /// The local workload is bound to the exact server-owned IAM profile and never wildcard authority.
    /// </summary>
    [Fact]
    public void Contract_UsesExactAuthServiceV1ProfileWithoutWildcardOrPlatformOwner()
    {
        Assert.Equal("auth-service", LocalServiceIdentitySeedOptions.WorkloadId);
        Assert.Equal(1, LocalServiceIdentitySeedOptions.ProfileVersion);
        Assert.Equal("roles.workloads.auth-service.v1", LocalServiceIdentitySeedOptions.RoleId);
        Assert.Equal("iam.auth.resolve-permissions", LocalServiceIdentitySeedOptions.WorkloadPermission);
        Assert.Equal("iam.workload-principals.provision", LocalServiceIdentitySeedOptions.ProvisionPermission);
        Assert.DoesNotContain('*', LocalServiceIdentitySeedOptions.RoleId);
        Assert.DoesNotContain("platform.owner", LocalServiceIdentitySeedOptions.RoleId, StringComparison.OrdinalIgnoreCase);
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
        Assert.Equal(new string('A', 64), options.SecretHash);
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
                ["AspireLocalServiceIdentity:SecretHash"] = hash
            })
            .Build();
}
