using System.Security.Cryptography;
using System.Text;
using Maliev.Aspire.DatabaseSeeder.Seeding.Services.Shared;
using Microsoft.Extensions.Configuration;

namespace Maliev.Aspire.LocalIdentity.Tests;

/// <summary>
/// Fast contract tests that compile the production Aspire-local identity implementation directly.
/// </summary>
public sealed class LocalServiceIdentityContractTests
{
    /// <summary>
    /// LifecycleService must retain its exact canonical identity and a unique reconciliation operation.
    /// </summary>
    [Fact]
    public void LifecycleProfile_HasCanonicalValuesAndUniqueProvisionOperation()
    {
        var lifecycle = LocalServiceIdentityProfileCatalog.LifecycleService;

        Assert.Equal("lifecycle-service", lifecycle.WorkloadId);
        Assert.Equal("service-lifecycle-service", lifecycle.ClientId);
        Assert.Equal("LifecycleService", lifecycle.ServiceName);
        Assert.Equal(1, lifecycle.ProfileVersion);
        Assert.Equal("roles.workloads.lifecycle-service.v1", lifecycle.RoleId);
        Assert.Equal(new Guid("8c0c8fa4-f62a-46f2-9aad-307e9027ae06"), lifecycle.ProvisionOperationId);
        Assert.Equal(10, LocalServiceIdentityProfileCatalog.All.Count);
        Assert.Equal(
            LocalServiceIdentityProfileCatalog.All.Count,
            LocalServiceIdentityProfileCatalog.All.Select(profile => profile.WorkloadId).Distinct().Count());
        Assert.Equal(
            LocalServiceIdentityProfileCatalog.All.Count,
            LocalServiceIdentityProfileCatalog.All.Select(profile => profile.ProvisionOperationId).Distinct().Count());
    }

    /// <summary>
    /// Every catalog build must create independent 256-bit Base64Url secrets and matching uppercase verifiers.
    /// </summary>
    [Fact]
    public void CreateCatalog_GeneratesIndependentMaterialWithMatchingVerifierHashes()
    {
        var catalog = LocalServiceIdentitySeedMaterial.CreateCatalog();

        Assert.Equal(LocalServiceIdentityProfileCatalog.All.Count, catalog.Count);
        Assert.Equal(catalog.Count, catalog.Values.Select(material => material.RawSecret).Distinct().Count());
        Assert.Equal(catalog.Count, catalog.Values.Select(material => material.SecretHash).Distinct().Count());
        Assert.All(catalog.Values, material =>
        {
            Assert.Equal(43, material.RawSecret.Length);
            Assert.All(material.RawSecret, character =>
                Assert.True(char.IsAsciiLetterOrDigit(character) || character is '-' or '_'));
            Assert.Equal(
                Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(material.RawSecret))),
                material.SecretHash);
            Assert.Matches("^[0-9A-F]{64}$", material.SecretHash);
        });
    }

    /// <summary>
    /// Enabled local configuration must retain verifier hashes only and reject non-local environments.
    /// </summary>
    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void FromConfiguration_LocalEnvironment_LoadsOnlyValidatedVerifierHashes(string environmentName)
    {
        var materials = LocalServiceIdentitySeedMaterial.CreateCatalog();
        var values = new Dictionary<string, string?>
        {
            [$"{LocalServiceIdentitySeedOptions.SectionName}:Enabled"] = "true"
        };
        foreach (var profile in LocalServiceIdentityProfileCatalog.All)
        {
            values[$"{LocalServiceIdentitySeedOptions.SectionName}:Profiles:{profile.WorkloadId}:SecretHash"] =
                materials[profile.WorkloadId].SecretHash;
        }

        var options = LocalServiceIdentitySeedOptions.FromConfiguration(
            new ConfigurationBuilder().AddInMemoryCollection(values).Build(),
            environmentName);

        Assert.True(options.Enabled);
        Assert.Equal(LocalServiceIdentityProfileCatalog.All.Count, options.SecretHashes.Count);
        Assert.All(LocalServiceIdentityProfileCatalog.All, profile =>
        {
            var material = materials[profile.WorkloadId];
            Assert.Equal(material.SecretHash, options.GetSecretHash(profile));
            Assert.DoesNotContain(material.RawSecret, options.SecretHashes.Values);
        });
    }

    /// <summary>
    /// Production-like environments must fail closed before any verifier is accepted.
    /// </summary>
    [Theory]
    [InlineData("Staging")]
    [InlineData("Production")]
    public void FromConfiguration_NonLocalEnvironment_RejectsEnabledIdentitySeeding(string environmentName)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{LocalServiceIdentitySeedOptions.SectionName}:Enabled"] = "true"
            })
            .Build();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            LocalServiceIdentitySeedOptions.FromConfiguration(configuration, environmentName));

        Assert.Contains("Development or Testing", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// A local configuration with malformed or missing verifier material must fail closed.
    /// </summary>
    [Fact]
    public void FromConfiguration_MalformedVerifier_FailsClosed()
    {
        var values = new Dictionary<string, string?>
        {
            [$"{LocalServiceIdentitySeedOptions.SectionName}:Enabled"] = "true"
        };
        foreach (var profile in LocalServiceIdentityProfileCatalog.All)
        {
            values[$"{LocalServiceIdentitySeedOptions.SectionName}:Profiles:{profile.WorkloadId}:SecretHash"] =
                new string('A', 64);
        }

        values[$"{LocalServiceIdentitySeedOptions.SectionName}:Profiles:lifecycle-service:SecretHash"] =
            new string('a', 64);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        Assert.Throws<InvalidOperationException>(() =>
            LocalServiceIdentitySeedOptions.FromConfiguration(configuration, "Testing"));
    }
}
