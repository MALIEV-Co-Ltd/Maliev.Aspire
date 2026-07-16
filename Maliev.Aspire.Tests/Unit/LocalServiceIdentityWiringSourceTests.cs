namespace Maliev.Aspire.Tests.Unit;

/// <summary>
/// Source-level guards for secret isolation and local-only AppHost orchestration.
/// </summary>
public sealed class LocalServiceIdentityWiringSourceTests
{
    /// <summary>
    /// AppHost must generate the material only for local environments and pass plaintext only to AuthService.
    /// </summary>
    [Fact]
    public void AppHost_WiresLocalIdentityWithoutDistributingPlaintextToSeederOrOtherServices()
    {
        var source = File.ReadAllText(FindSource("Maliev.Aspire.AppHost", "AppHost.cs"));

        Assert.Contains("LocalServiceIdentitySeedMaterial.Create()", source, StringComparison.Ordinal);
        Assert.Contains("IsLocalEnvironment(environmentName)", source, StringComparison.Ordinal);
        Assert.Contains("\"ServiceAuthentication__ClientId\"", source, StringComparison.Ordinal);
        Assert.Contains("\"ServiceAuthentication__ClientSecret\"", source, StringComparison.Ordinal);
        Assert.Contains("LocalServiceIdentitySeedOptions.ClientId", source, StringComparison.Ordinal);
        Assert.Contains("localIdentitySecret", source, StringComparison.Ordinal);
        Assert.Contains("\"AspireLocalServiceIdentity__SecretHash\"", source, StringComparison.Ordinal);
        Assert.Contains("localIdentityMaterial.SecretHash", source, StringComparison.Ordinal);

        var rawSecretEnvironmentOccurrences = CountOccurrences(
            source,
            "\"ServiceAuthentication__ClientSecret\"");
        Assert.Equal(1, rawSecretEnvironmentOccurrences);

        var verifierEnvironmentOccurrences = CountOccurrences(
            source,
            "\"AspireLocalServiceIdentity__SecretHash\"");
        Assert.Equal(1, verifierEnvironmentOccurrences);
    }

    /// <summary>
    /// The child seeder environment copy must explicitly strip the raw client secret.
    /// </summary>
    [Fact]
    public void SeederExtension_RemovesRawSecretAndSupportsLocalAutomaticCompletion()
    {
        var source = File.ReadAllText(FindSource(
            "Maliev.Aspire.AppHost",
            "Extensions",
            "MalievResourceExtensions.cs"));

        Assert.Contains("runAutomatically", source, StringComparison.Ordinal);
        Assert.Contains("ServiceAuthentication__ClientSecret", source, StringComparison.Ordinal);
        Assert.Contains("context.EnvironmentVariables.Remove", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// IAM local setup must use a distinct exact role and invoke the canonical provisioner.
    /// </summary>
    [Fact]
    public void IamSeeder_UsesExactProvisionerGrantAndCanonicalWorkloadProvisioner()
    {
        var source = File.ReadAllText(FindSource(
            "Maliev.Aspire.DatabaseSeeder",
            "Seeding",
            "Services",
            "IAMService",
            "IAMDatabaseSeeder.cs"));

        Assert.Contains("LocalServiceIdentitySeedOptions.ProvisionerRoleId", source, StringComparison.Ordinal);
        Assert.Contains("LocalServiceIdentitySeedOptions.ProvisionPermission", source, StringComparison.Ordinal);
        Assert.Contains("IWorkloadPrincipalProvisioner", source, StringComparison.Ordinal);
        Assert.Contains("ProvisionAsync(", source, StringComparison.Ordinal);
        Assert.Contains("ResourcePath = null", source, StringComparison.Ordinal);
        Assert.DoesNotContain(
            "LocalServiceIdentitySeedOptions.ProvisionerPrincipalId,\n            RoleId = PlatformOwnerRoleId",
            source.ReplaceLineEndings("\n"),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// Auth seeding must consume only the verifier and persist a versioned active credential.
    /// </summary>
    [Fact]
    public void AuthSeeder_ConsumesHashOnlyAndValidatesExactIamState()
    {
        var source = File.ReadAllText(FindSource(
            "Maliev.Aspire.DatabaseSeeder",
            "Seeding",
            "Services",
            "AuthService",
            "AuthServiceIdentityDatabaseSeeder.cs"));

        Assert.Contains("options.SecretHash", source, StringComparison.Ordinal);
        Assert.Contains("ServiceCredentialVersionStatus.Active", source, StringComparison.Ordinal);
        Assert.Contains("Context.ServiceCredentialVersions", source, StringComparison.Ordinal);
        Assert.Contains("LocalServiceIdentitySeedOptions.WorkloadPermission", source, StringComparison.Ordinal);
        Assert.Contains("LocalServiceIdentitySeedOptions.RoleId", source, StringComparison.Ordinal);
        Assert.Contains("IsolationLevel.Serializable", source, StringComparison.Ordinal);
        Assert.Contains("pg_advisory_xact_lock", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RawSecret", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ClientSecret =", source, StringComparison.Ordinal);
    }

    private static int CountOccurrences(string value, string search)
    {
        var count = 0;
        var index = 0;
        while ((index = value.IndexOf(search, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += search.Length;
        }

        return count;
    }

    private static string FindSource(params string[] relativeSegments)
    {
        foreach (var startDirectory in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var directory = new DirectoryInfo(startDirectory);
            while (directory is not null)
            {
                var candidate = Path.Combine([directory.FullName, .. relativeSegments]);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        throw new FileNotFoundException($"Unable to locate {string.Join('/', relativeSegments)}.");
    }
}
