namespace Maliev.Aspire.Tests.Unit;

/// <summary>
/// Source-level guards for secret isolation and local-only AppHost orchestration.
/// </summary>
public sealed class LocalServiceIdentityWiringSourceTests
{
    /// <summary>
    /// AppHost must isolate the ephemeral capability private key to Auth and its public ring to IAM.
    /// </summary>
    [Fact]
    public void AppHost_WiresTokenIssuanceCapabilityOnlyToAuthAndIamInLocalEnvironments()
    {
        var source = File.ReadAllText(FindSource("Maliev.Aspire.AppHost", "AppHost.cs"));
        var compactSource = string.Concat(source.Where(character => !char.IsWhiteSpace(character)));

        Assert.Contains("LocalTokenIssuanceCapabilityMaterial.CreateForEnvironment(environmentName)", source, StringComparison.Ordinal);
        Assert.Contains("if (isLocalEnvironment)", source, StringComparison.Ordinal);
        Assert.Contains("\"Auth__TokenIssuanceCapability__ActiveKeyId\"", source, StringComparison.Ordinal);
        Assert.Contains("\"Auth__TokenIssuanceCapability__PrivateKey\"", source, StringComparison.Ordinal);
        Assert.Contains("\"AuthTokenIssuanceCapabilityPrivateKey\"", source, StringComparison.Ordinal);
        Assert.Contains("localTokenIssuanceCapabilityPrivateKey!", source, StringComparison.Ordinal);
        Assert.Contains("localTokenIssuanceCapability.PrivateKeyPem", source, StringComparison.Ordinal);
        Assert.Contains("$\"IAM__TokenIssuanceCapability__PublicKeys__{localTokenIssuanceCapability.ActiveKeyId}\"", source, StringComparison.Ordinal);
        Assert.Contains("localTokenIssuanceCapability.PublicKey", source, StringComparison.Ordinal);
        Assert.Equal(2, CountOccurrences(source, "\"Auth__TokenIssuanceCapability__PrivateKey\""));
        Assert.Equal(2, CountOccurrences(source, "IAM__TokenIssuanceCapability__PublicKeys__"));
        Assert.DoesNotContain(
            "Auth__TokenIssuanceCapability__PrivateKey\", config.JwtPrivateKey",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain(
            "\"Auth__TokenIssuanceCapability__PrivateKey\",localTokenIssuanceCapability.PrivateKeyPem",
            compactSource,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// AppHost must generate the material only for local environments and pass plaintext only to AuthService.
    /// </summary>
    [Fact]
    public void AppHost_WiresLocalIdentityWithoutDistributingPlaintextToSeederOrOtherServices()
    {
        var source = File.ReadAllText(FindSource("Maliev.Aspire.AppHost", "AppHost.cs"));

        Assert.Contains("LocalServiceIdentitySeedMaterial.CreateCatalog()", source, StringComparison.Ordinal);
        Assert.Contains("IsLocalEnvironment(environmentName)", source, StringComparison.Ordinal);
        Assert.Contains("\"ServiceAuthentication__ClientId\"", source, StringComparison.Ordinal);
        Assert.Contains("\"ServiceAuthentication__ClientSecret\"", source, StringComparison.Ordinal);
        Assert.Contains("LocalServiceIdentityProfileCatalog.AuthService.ClientId", source, StringComparison.Ordinal);
        Assert.Contains("LocalServiceIdentityProfileCatalog.ContactService.ClientId", source, StringComparison.Ordinal);
        Assert.Contains("LocalServiceIdentityProfileCatalog.AccountingService.ClientId", source, StringComparison.Ordinal);
        Assert.Contains("LocalServiceIdentityProfileCatalog.PricingService.ClientId", source, StringComparison.Ordinal);
        Assert.Contains("LocalServiceIdentityProfileCatalog.MaterialService.ClientId", source, StringComparison.Ordinal);
        Assert.Contains("LocalServiceIdentityProfileCatalog.LifecycleService.ClientId", source, StringComparison.Ordinal);
        Assert.Contains("localIdentitySecrets", source, StringComparison.Ordinal);
        Assert.Contains("AspireLocalServiceIdentity__Profiles__auth-service__SecretHash", source, StringComparison.Ordinal);
        Assert.Contains("AspireLocalServiceIdentity__Profiles__contact-service__SecretHash", source, StringComparison.Ordinal);
        Assert.Contains("AspireLocalServiceIdentity__Profiles__search-service__SecretHash", source, StringComparison.Ordinal);
        Assert.Contains("AspireLocalServiceIdentity__Profiles__registry-service__SecretHash", source, StringComparison.Ordinal);
        Assert.Contains("AspireLocalServiceIdentity__Profiles__country-service__SecretHash", source, StringComparison.Ordinal);
        Assert.Contains("AspireLocalServiceIdentity__Profiles__currency-service__SecretHash", source, StringComparison.Ordinal);
        Assert.Contains("AspireLocalServiceIdentity__Profiles__accounting-service__SecretHash", source, StringComparison.Ordinal);
        Assert.Contains("AspireLocalServiceIdentity__Profiles__pricing-service__SecretHash", source, StringComparison.Ordinal);
        Assert.Contains("AspireLocalServiceIdentity__Profiles__material-service__SecretHash", source, StringComparison.Ordinal);
        Assert.Contains("AspireLocalServiceIdentity__Profiles__lifecycle-service__SecretHash", source, StringComparison.Ordinal);
        Assert.Contains("localIdentitySecretHashes", source, StringComparison.Ordinal);

        Assert.Contains("AuthServiceLocalClientSecret", source, StringComparison.Ordinal);
        Assert.Contains("ContactServiceLocalClientSecret", source, StringComparison.Ordinal);
        Assert.Contains(
            "config.LocalServiceIdentitySecrets[",
            source,
            StringComparison.Ordinal);

        var verifierEnvironmentOccurrences = CountOccurrences(
            source,
            "__SecretHash\"");
        Assert.Equal(20, verifierEnvironmentOccurrences);
    }

    /// <summary>
    /// ContactService must exchange its own credential with AuthService and retain verifier-only JWT settings.
    /// </summary>
    [Fact]
    public void AppHost_WiresContactToAuthAndRemovesLocalSigningMaterial()
    {
        var source = File.ReadAllText(FindSource("Maliev.Aspire.AppHost", "AppHost.cs"));

        var contactStart = source.IndexOf("var contactService =", StringComparison.Ordinal);
        var currencyStart = source.IndexOf("var currencyService =", contactStart, StringComparison.Ordinal);
        Assert.True(contactStart >= 0 && currencyStart > contactStart);
        var contactBlock = source[contactStart..currencyStart];
        Assert.Contains("WithReference(authService)", contactBlock, StringComparison.Ordinal);
        Assert.Contains("WaitFor(authService)", contactBlock, StringComparison.Ordinal);
        Assert.Contains("LocalServiceIdentityProfileCatalog.ContactService.ClientId", contactBlock, StringComparison.Ordinal);
        Assert.Contains("WithoutJwtSigningMaterial()", contactBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("Jwt__PrivateKey", contactBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("Jwt__SecurityKey", contactBlock, StringComparison.Ordinal);
    }

    /// <summary>
    /// AccountingService must exchange its own credential with AuthService and retain verifier-only JWT settings.
    /// </summary>
    [Fact]
    public void AppHost_WiresAccountingToAuthAndRemovesLocalSigningMaterial()
    {
        var source = File.ReadAllText(FindSource("Maliev.Aspire.AppHost", "AppHost.cs"));

        var accountingStart = source.IndexOf("var accountingService =", StringComparison.Ordinal);
        var notificationStart = source.IndexOf("var notificationService =", accountingStart, StringComparison.Ordinal);
        Assert.True(accountingStart >= 0 && notificationStart > accountingStart);
        var accountingBlock = source[accountingStart..notificationStart];
        Assert.Contains("WithReference(authService)", accountingBlock, StringComparison.Ordinal);
        Assert.Contains("WaitFor(authService)", accountingBlock, StringComparison.Ordinal);
        Assert.Contains("LocalServiceIdentityProfileCatalog.AccountingService.ClientId", accountingBlock, StringComparison.Ordinal);
        Assert.Contains("LocalServiceIdentityProfileCatalog.AccountingService.WorkloadId", accountingBlock, StringComparison.Ordinal);
        Assert.Contains("WithoutJwtSigningMaterial()", accountingBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("Jwt__PrivateKey", accountingBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("Jwt__SecurityKey", accountingBlock, StringComparison.Ordinal);
    }

    /// <summary>
    /// PricingService must exchange its own credential with AuthService and retain verifier-only JWT settings.
    /// </summary>
    [Fact]
    public void AppHost_WiresPricingToAuthAndRemovesLocalSigningMaterial()
    {
        var source = File.ReadAllText(FindSource("Maliev.Aspire.AppHost", "AppHost.cs"));

        var pricingStart = source.IndexOf("var pricingService =", StringComparison.Ordinal);
        var orderStart = source.IndexOf("var orderService =", pricingStart, StringComparison.Ordinal);
        Assert.True(pricingStart >= 0 && orderStart > pricingStart);
        var pricingBlock = source[pricingStart..orderStart];
        Assert.Contains("WithReference(authService)", pricingBlock, StringComparison.Ordinal);
        Assert.Contains("WaitFor(authService)", pricingBlock, StringComparison.Ordinal);
        Assert.Contains("LocalServiceIdentityProfileCatalog.PricingService.ClientId", pricingBlock, StringComparison.Ordinal);
        Assert.Contains("LocalServiceIdentityProfileCatalog.PricingService.WorkloadId", pricingBlock, StringComparison.Ordinal);
        Assert.Contains("WithoutJwtSigningMaterial()", pricingBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("Jwt__PrivateKey", pricingBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("Jwt__SecurityKey", pricingBlock, StringComparison.Ordinal);
    }

    /// <summary>
    /// MaterialService must exchange only its own credential and wait for Auth and Supplier.
    /// </summary>
    [Fact]
    public void AppHost_WiresMaterialToAuthAndSupplierWithoutLocalSigningMaterial()
    {
        var source = File.ReadAllText(FindSource("Maliev.Aspire.AppHost", "AppHost.cs"));

        var materialStart = source.IndexOf("var materialService =", StringComparison.Ordinal);
        var pricingStart = source.IndexOf("var pricingService =", materialStart, StringComparison.Ordinal);
        Assert.True(materialStart >= 0 && pricingStart > materialStart);
        var materialBlock = source[materialStart..pricingStart];
        Assert.Contains("WithReference(authService)", materialBlock, StringComparison.Ordinal);
        Assert.Contains("WaitFor(authService)", materialBlock, StringComparison.Ordinal);
        Assert.Contains("LocalServiceIdentityProfileCatalog.MaterialService.ClientId", materialBlock, StringComparison.Ordinal);
        Assert.Contains("LocalServiceIdentityProfileCatalog.MaterialService.WorkloadId", materialBlock, StringComparison.Ordinal);
        Assert.Contains("WithoutJwtSigningMaterial()", materialBlock, StringComparison.Ordinal);
        Assert.Equal(
            1,
            CountOccurrences(source, "LocalServiceIdentityProfileCatalog.MaterialService.ClientId"));
        Assert.DoesNotContain("Jwt__PrivateKey", materialBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("Jwt__SecurityKey", materialBlock, StringComparison.Ordinal);

        var supplierStart = source.IndexOf("var supplierService =", StringComparison.Ordinal);
        var chatbotStart = source.IndexOf("var chatbotService =", supplierStart, StringComparison.Ordinal);
        Assert.True(supplierStart >= 0 && chatbotStart > supplierStart);
        var supplierBlock = source[supplierStart..chatbotStart];
        Assert.Contains("materialService = materialService", supplierBlock, StringComparison.Ordinal);
        Assert.Contains("WithReference(supplierService)", supplierBlock, StringComparison.Ordinal);
        Assert.Contains("WaitFor(supplierService)", supplierBlock, StringComparison.Ordinal);
    }

    /// <summary>
    /// LifecycleService must wait for Auth, exchange only its own local credential, and retain verifier-only JWT settings.
    /// </summary>
    [Fact]
    public void AppHost_WiresLifecycleToAuthWithoutLocalSigningMaterial()
    {
        var source = File.ReadAllText(FindSource("Maliev.Aspire.AppHost", "AppHost.cs"));

        var lifecycleStart = source.IndexOf("var lifecycleService =", StringComparison.Ordinal);
        var performanceStart = source.IndexOf("var performanceService =", lifecycleStart, StringComparison.Ordinal);
        Assert.True(lifecycleStart >= 0 && performanceStart > lifecycleStart);
        var lifecycleBlock = source[lifecycleStart..performanceStart];
        Assert.Contains("WithReference(authService)", lifecycleBlock, StringComparison.Ordinal);
        Assert.Contains("WaitFor(authService)", lifecycleBlock, StringComparison.Ordinal);
        Assert.Contains("LocalServiceIdentityProfileCatalog.LifecycleService.ClientId", lifecycleBlock, StringComparison.Ordinal);
        Assert.Contains("LocalServiceIdentityProfileCatalog.LifecycleService.WorkloadId", lifecycleBlock, StringComparison.Ordinal);
        Assert.Contains("WithoutJwtSigningMaterial()", lifecycleBlock, StringComparison.Ordinal);
        Assert.Contains(
            "if (isLocalEnvironment && config.LocalServiceIdentitySecrets is not null)",
            lifecycleBlock,
            StringComparison.Ordinal);
        Assert.Equal(
            1,
            CountOccurrences(source, "LocalServiceIdentityProfileCatalog.LifecycleService.ClientId"));
        Assert.DoesNotContain("Jwt__PrivateKey", lifecycleBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("Jwt__SecurityKey", lifecycleBlock, StringComparison.Ordinal);
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
        Assert.Contains("Auth__TokenIssuanceCapability__PrivateKey", source, StringComparison.Ordinal);
        Assert.Contains("IAM__TokenIssuanceCapability__PublicKeys__", source, StringComparison.Ordinal);
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

        Assert.Contains("options.GetSecretHash(profile)", source, StringComparison.Ordinal);
        Assert.Contains("ServiceCredentialVersionStatus.Active", source, StringComparison.Ordinal);
        Assert.Contains("Context.ServiceCredentialVersions", source, StringComparison.Ordinal);
        Assert.Contains("LocalServiceIdentityProfileCatalog.All", source, StringComparison.Ordinal);
        Assert.Contains("WorkloadAccessProfileCatalog.Default.Get", source, StringComparison.Ordinal);
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
