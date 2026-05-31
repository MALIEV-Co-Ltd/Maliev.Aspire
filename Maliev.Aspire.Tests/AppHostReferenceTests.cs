using System.Text.Json;

namespace Maliev.Aspire.Tests;

/// <summary>
/// Tests for AppHost service discovery references used by cross-service workflows.
/// </summary>
public sealed class AppHostReferenceTests
{
    /// <summary>
    /// QuotationService must be able to resolve CustomerService during project quote creation.
    /// </summary>
    [Fact]
    public void AppHost_QuotationService_ReferencesCustomerService()
    {
        var appHostSource = File.ReadAllText(FindAppHostSource());
        var quotationBlockStart = appHostSource.IndexOf(
            "var quotationService = WithSharedSecrets(",
            StringComparison.Ordinal);
        var invoiceBlockStart = appHostSource.IndexOf(
            "var invoiceService = WithSharedSecrets(",
            StringComparison.Ordinal);

        Assert.True(quotationBlockStart >= 0, "QuotationService resource declaration was not found.");
        Assert.True(invoiceBlockStart > quotationBlockStart, "InvoiceService resource declaration was not found after QuotationService.");

        var quotationBlock = appHostSource[quotationBlockStart..invoiceBlockStart];
        Assert.Contains(".WithReference(customerService)", quotationBlock, StringComparison.Ordinal);
    }

    /// <summary>
    /// GeometryService must not start OTLP export before the collector is ready outside Testing.
    /// </summary>
    [Fact]
    public void AppHost_GeometryService_WaitsForOpenTelemetryCollector()
    {
        var appHostSource = File.ReadAllText(FindAppHostSource());
        var geometryBlockStart = appHostSource.IndexOf(
            "var geometryService = builder.AddDockerfile(",
            StringComparison.Ordinal);
        var bffReferenceStart = appHostSource.IndexOf(
            "intranetBff = intranetBff.WithReference(geometryService.GetEndpoint(\"http\"));",
            StringComparison.Ordinal);

        Assert.True(geometryBlockStart >= 0, "GeometryService resource declaration was not found.");
        Assert.True(bffReferenceStart > geometryBlockStart, "GeometryService reference block was not found after GeometryService.");

        var geometryBlock = appHostSource[geometryBlockStart..bffReferenceStart];
        Assert.Contains(".WithEnvironment(\"OTEL_EXPORTER_OTLP_ENDPOINT\", otelCollector.GetEndpoint(\"grpc\"))", geometryBlock, StringComparison.Ordinal);
        Assert.Contains("if (!environmentName.Equals(\"Testing\", StringComparison.OrdinalIgnoreCase))", geometryBlock, StringComparison.Ordinal);
        Assert.Contains("geometryService = geometryService.WaitFor(otelCollector);", geometryBlock, StringComparison.Ordinal);
    }

    /// <summary>
    /// OpenTelemetry collector config must not depend on Docker Desktop host-drive bind mounts.
    /// </summary>
    [Fact]
    public void AppHost_OpenTelemetryCollector_UsesContainerFileConfig()
    {
        var appHostSource = File.ReadAllText(FindAppHostSource());
        var appHostDirectory = Path.GetDirectoryName(FindAppHostSource())!;
        var collectorExtensionSource = File.ReadAllText(Path.Combine(
            appHostDirectory,
            "OpenTelemetryCollector",
            "OpenTelemetryCollectorResourceBuilderExtensions.cs"));

        Assert.Contains("AddOpenTelemetryCollector(\"otelcollector\", \"../otelcollector/config.yaml\")", appHostSource, StringComparison.Ordinal);
        Assert.Contains(".WithContainerFiles(\"/etc/otelcol-contrib\"", collectorExtensionSource, StringComparison.Ordinal);
        Assert.Contains("Contents = File.ReadAllText(configFilePath)", collectorExtensionSource, StringComparison.Ordinal);
        Assert.DoesNotContain(".WithBindMount(configFileLocation", collectorExtensionSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// Monitoring containers should avoid host bind mounts so Docker Desktop drive sharing cannot block the app model.
    /// </summary>
    [Fact]
    public void AppHost_MonitoringContainers_AvoidHostBindMounts()
    {
        var appHostSource = File.ReadAllText(FindAppHostSource());
        var appHostDirectory = Path.GetDirectoryName(FindAppHostSource())!;
        var collectorExtensionSource = File.ReadAllText(Path.Combine(
            appHostDirectory,
            "OpenTelemetryCollector",
            "OpenTelemetryCollectorResourceBuilderExtensions.cs"));
        var pathResolverSource = File.ReadAllText(Path.Combine(
            appHostDirectory,
            "AppHostPathResolver.cs"));

        Assert.Contains(".WithContainerFiles(\"/etc/prometheus\", AppHostPathResolver.ResolveRequiredDirectoryPath(\"../prometheus\"))", appHostSource, StringComparison.Ordinal);
        Assert.Contains(".WithContainerFiles(\"/etc/grafana\", AppHostPathResolver.ResolveRequiredDirectoryPath(\"../grafana/config\"))", appHostSource, StringComparison.Ordinal);
        Assert.Contains(".WithContainerFiles(\"/var/lib/grafana/dashboards\", AppHostPathResolver.ResolveRequiredDirectoryPath(\"../grafana/dashboards\"))", appHostSource, StringComparison.Ordinal);
        Assert.Contains("insight.WithVolume(\"redisinsight-data\", \"/data\")", appHostSource, StringComparison.Ordinal);
        Assert.Contains("AppHostPathResolver.ResolveRequiredFilePath(configFileLocation)", collectorExtensionSource, StringComparison.Ordinal);
        Assert.Contains("\"Maliev.Aspire.AppHost\", sourcePath", pathResolverSource, StringComparison.Ordinal);
        Assert.DoesNotContain(".WithBindMount(\"../prometheus\"", appHostSource, StringComparison.Ordinal);
        Assert.DoesNotContain(".WithBindMount(\"../grafana", appHostSource, StringComparison.Ordinal);
        Assert.DoesNotContain("insight.WithBindMount(\"redisinsight-data\"", appHostSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// API service links should have meaningful Scalar labels in the Aspire dashboard.
    /// </summary>
    [Fact]
    public void AppHost_ApiServices_ExposeNamedScalarLinks()
    {
        var appHostSource = File.ReadAllText(FindAppHostSource());

        Assert.Contains(".WithServiceScalarUrl()", appHostSource, StringComparison.Ordinal);
        Assert.Contains("[\"AccountingService\"] = new(\"/accounting/scalar\", \"Accounting Scalar\")", appHostSource, StringComparison.Ordinal);
        Assert.Contains("[\"PurchaseOrderService\"] = new(\"/purchase-order/scalar\", \"Purchase Order Scalar\")", appHostSource, StringComparison.Ordinal);
        Assert.Contains("[\"SearchService\"] = new(\"/search/scalar\", \"Search Scalar\")", appHostSource, StringComparison.Ordinal);
        Assert.Contains("u.DisplayText = \"Geometry Scalar\"", appHostSource, StringComparison.Ordinal);
        Assert.DoesNotContain("DisplayText = \"Scalar Documentation\"", appHostSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// Aspire should expose explicit seed commands for the local test administrator login path.
    /// </summary>
    [Fact]
    public void AppHost_LocalTestAdmin_RegistersEmployeeAndIamSeeders()
    {
        var appHostSource = File.ReadAllText(FindAppHostSource());
        var appHostDirectory = Path.GetDirectoryName(FindAppHostSource())!;
        var resourceExtensionSource = File.ReadAllText(Path.Combine(
            appHostDirectory,
            "Extensions",
            "MalievResourceExtensions.cs"));

        Assert.Contains(".SeedDatabase<CountryDatabaseSeeder>(databases.Country", appHostSource, StringComparison.Ordinal);
        Assert.Contains(".SeedDatabase<EmployeeDatabaseSeeder>(databases.Employee", appHostSource, StringComparison.Ordinal);
        Assert.Contains(".SeedDatabase<IAMDatabaseSeeder>(databases.IAM", appHostSource, StringComparison.Ordinal);
        Assert.Contains(".AddExecutable(", resourceExtensionSource, StringComparison.Ordinal);
        Assert.Contains("ResolveSeederAssemblyPath()", resourceExtensionSource, StringComparison.Ordinal);
        Assert.Contains("targetService.WaitForCompletion(seeder);", resourceExtensionSource, StringComparison.Ordinal);
        Assert.DoesNotContain("../Maliev.Aspire.DatabaseSeeder/Maliev.Aspire.DatabaseSeeder.csproj", resourceExtensionSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// The Intranet customer seed dashboard command calls a protected BFF endpoint.
    /// </summary>
    [Fact]
    public void AppHost_IntranetCustomerSeedCommand_AddsServiceAccountBearerToken()
    {
        var appHostSource = File.ReadAllText(FindAppHostSource());
        var commandStart = appHostSource.IndexOf(
            "path: \"/api/v1/seed/customers\"",
            StringComparison.Ordinal);
        var nextResourceStart = appHostSource.IndexOf(
            "quoteEngineBff",
            commandStart,
            StringComparison.Ordinal);

        Assert.True(commandStart >= 0, "Intranet customer seed command was not found.");
        Assert.True(nextResourceStart > commandStart, "Intranet customer seed command block end was not found.");

        var commandBlock = appHostSource[commandStart..nextResourceStart];
        Assert.Contains("PrepareRequest", commandBlock, StringComparison.Ordinal);
        Assert.Contains("ServiceAccountTokenProvider(builder.Configuration, \"IntranetBff\")", commandBlock, StringComparison.Ordinal);
        Assert.Contains("AuthenticationHeaderValue(\"Bearer\", tokenProvider.GetToken())", commandBlock, StringComparison.Ordinal);
    }

    /// <summary>
    /// Customer creation paths require CountryService data before address forms are usable.
    /// </summary>
    [Fact]
    public void AppHost_CustomerAndIntranet_WaitForCountryReferenceData()
    {
        var appHostSource = File.ReadAllText(FindAppHostSource());
        var customerBlockStart = appHostSource.IndexOf(
            "var customerService = WithSharedSecrets(",
            StringComparison.Ordinal);
        var employeeBlockStart = appHostSource.IndexOf(
            "var employeeService = WithSharedSecrets(",
            StringComparison.Ordinal);
        var intranetBlockStart = appHostSource.IndexOf(
            "builder.AddProject<Projects.Maliev_Intranet_Bff>(\"IntranetBff\")",
            StringComparison.Ordinal);
        var webBlockStart = appHostSource.IndexOf(
            "builder.AddProject<Projects.Maliev_Web_Bff>(\"WebBff\")",
            StringComparison.Ordinal);

        Assert.True(customerBlockStart >= 0, "CustomerService resource declaration was not found.");
        Assert.True(employeeBlockStart > customerBlockStart, "EmployeeService resource declaration was not found after CustomerService.");
        Assert.True(intranetBlockStart >= 0, "IntranetBff resource declaration was not found.");
        Assert.True(webBlockStart > intranetBlockStart, "WebBff resource declaration was not found after IntranetBff.");

        var customerBlock = appHostSource[customerBlockStart..employeeBlockStart];
        var intranetBlock = appHostSource[intranetBlockStart..webBlockStart];
        Assert.Contains(".WithReference(countryService)", customerBlock, StringComparison.Ordinal);
        Assert.Contains(".WaitFor(countryService)", customerBlock, StringComparison.Ordinal);
        Assert.Contains(".WithReference(countryService)", intranetBlock, StringComparison.Ordinal);
        Assert.Contains(".WaitFor(countryService)", intranetBlock, StringComparison.Ordinal);
    }

    /// <summary>
    /// AuthService must wait for customer account dependencies before accepting customer Google exchanges.
    /// </summary>
    [Fact]
    public void AppHost_AuthService_WaitsForCustomerAccountDependencies()
    {
        var appHostSource = File.ReadAllText(FindAppHostSource());
        var authBlockStart = appHostSource.IndexOf(
            "var authService = WithSharedSecrets(",
            StringComparison.Ordinal);
        var accountingBlockStart = appHostSource.IndexOf(
            "var accountingService = WithSharedSecrets(",
            StringComparison.Ordinal);

        Assert.True(authBlockStart >= 0, "AuthService resource declaration was not found.");
        Assert.True(accountingBlockStart > authBlockStart, "AccountingService resource declaration was not found after AuthService.");

        var authBlock = appHostSource[authBlockStart..accountingBlockStart];
        Assert.Contains(".WithReference(customerService)", authBlock, StringComparison.Ordinal);
        Assert.Contains(".WaitFor(customerService)", authBlock, StringComparison.Ordinal);
        Assert.Contains(".WithReference(employeeService)", authBlock, StringComparison.Ordinal);
        Assert.Contains(".WaitFor(employeeService)", authBlock, StringComparison.Ordinal);
    }

    /// <summary>
    /// QuoteEngine BFF must be part of the Aspire AppHost project graph.
    /// </summary>
    [Fact]
    public void AppHost_ProjectReferences_IncludeQuoteEngineBff()
    {
        var appHostProject = File.ReadAllText(FindAppHostProjectFile());

        Assert.Contains("..\\..\\Maliev.QuoteEngine\\Maliev.QuoteEngine.Bff\\Maliev.QuoteEngine.Bff.csproj", appHostProject, StringComparison.Ordinal);
    }

    /// <summary>
    /// QuoteEngine BFF must be registered with the downstream services needed by the customer quote journey.
    /// </summary>
    [Fact]
    public void AppHost_QuoteEngineBff_ReferencesProductionQuoteJourneyServices()
    {
        var appHostSource = File.ReadAllText(FindAppHostSource());
        var quoteEngineBlockStart = appHostSource.IndexOf(
            "builder.AddProject<Projects.Maliev_QuoteEngine_Bff>(\"QuoteEngineBff\")",
            StringComparison.Ordinal);
        var webBlockStart = appHostSource.IndexOf(
            "builder.AddProject<Projects.Maliev_Web_Bff>(\"WebBff\")",
            StringComparison.Ordinal);

        Assert.True(quoteEngineBlockStart >= 0, "QuoteEngineBff resource declaration was not found.");
        Assert.True(webBlockStart > quoteEngineBlockStart, "WebBff resource declaration was not found after QuoteEngineBff.");

        var quoteEngineBlock = appHostSource[quoteEngineBlockStart..webBlockStart];
        foreach (var dependency in new[]
        {
            "infrastructure.RabbitMQ",
            "infrastructure.Redis",
            "authService",
            "iamService",
            "customerService",
            "uploadService",
            "materialService",
            "pricingService",
            "projectService",
            "quotationService",
            "pdfService",
            "orderService",
            "paymentService",
            "deliveryService",
            "chatbotService"
        })
        {
            Assert.Contains($".WithReference({dependency})", quoteEngineBlock, StringComparison.Ordinal);
        }

        Assert.Contains(".WaitFor(infrastructure.RabbitMQ)", quoteEngineBlock, StringComparison.Ordinal);
        Assert.Contains(".WithUrlForEndpoint(\"http\", u => u.DisplayText = \"Quote Engine (HTTP)\")", quoteEngineBlock, StringComparison.Ordinal);
        Assert.Contains(".WithUrlForEndpoint(\"https\", u => u.DisplayText = \"Quote Engine (HTTPS)\")", quoteEngineBlock, StringComparison.Ordinal);
        Assert.Contains(".WithTestingSafeHttpHealthCheck(\"/quote/aspire-liveness\")", quoteEngineBlock, StringComparison.Ordinal);
    }

    /// <summary>
    /// QuoteEngine BFF must receive GeometryService endpoint discovery for CAD analysis flows.
    /// </summary>
    [Fact]
    public void AppHost_QuoteEngineBff_ReferencesGeometryServiceEndpoint()
    {
        var appHostSource = File.ReadAllText(FindAppHostSource());

        Assert.Contains("quoteEngineBff = quoteEngineBff.WithReference(geometryService.GetEndpoint(\"http\"));", appHostSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// Customer Web BFF must be registered with the downstream services used by its BFF clients.
    /// </summary>
    [Fact]
    public void AppHost_WebBff_ReferencesCustomerFacingServices()
    {
        var appHostSource = File.ReadAllText(FindAppHostSource());
        var webBlockStart = appHostSource.IndexOf(
            "builder.AddProject<Projects.Maliev_Web_Bff>(\"WebBff\")",
            StringComparison.Ordinal);
        var inventoryBlockStart = appHostSource.IndexOf(
            "var inventoryService = WithSharedSecrets(",
            StringComparison.Ordinal);

        Assert.True(webBlockStart >= 0, "WebBff resource declaration was not found.");
        Assert.True(inventoryBlockStart > webBlockStart, "InventoryService resource declaration was not found after WebBff.");

        var webBlock = appHostSource[webBlockStart..inventoryBlockStart];
        foreach (var dependency in new[]
        {
            "authService",
            "iamService",
            "customerService",
            "countryService",
            "contactService",
            "deliveryService",
            "materialService",
            "orderService",
            "paymentService",
            "pricingService",
            "uploadService",
            "commerceService",
            "chatbotService"
        })
        {
            Assert.Contains($".WithReference({dependency})", webBlock, StringComparison.Ordinal);
        }

        Assert.Contains(".WithEnvironment(\"QuoteEngine__BaseUrl\", quoteEngineBff.GetEndpoint(\"https\"))", webBlock, StringComparison.Ordinal);
        Assert.Contains(".WaitFor(authService)", webBlock, StringComparison.Ordinal);
        Assert.Contains(".WaitFor(customerService)", webBlock, StringComparison.Ordinal);
        Assert.Contains(".WithEnvironment(\"Authentication__Google__ClientId\", config.WebGoogleClientId)", webBlock, StringComparison.Ordinal);
        Assert.Contains(".WithEnvironment(\"Authentication__Google__ClientSecret\", config.WebGoogleClientSecret)", webBlock, StringComparison.Ordinal);
        Assert.Contains(".WithTestingSafeHttpHealthCheck(\"/web/aspire-liveness\")", webBlock, StringComparison.Ordinal);
    }

    /// <summary>
    /// Customer Web BFF should use its own Google OAuth client instead of the employee Intranet client.
    /// </summary>
    [Fact]
    public void AppHost_WebBff_LoadsDedicatedGoogleOAuthConfiguration()
    {
        var appHostSource = File.ReadAllText(FindAppHostSource());

        Assert.Contains("Authentication:Google:Web:ClientId", appHostSource, StringComparison.Ordinal);
        Assert.Contains("Authentication:Google:Web:ClientSecret", appHostSource, StringComparison.Ordinal);
        Assert.Contains("WebGoogleClientId: webGoogleClientId", appHostSource, StringComparison.Ordinal);
        Assert.Contains("WebGoogleClientSecret: webGoogleClientSecret", appHostSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// Aspire browser E2E must use deterministic UploadService storage instead of external GCS credentials.
    /// </summary>
    [Fact]
    public void AppHost_UploadService_UsesMockStorageForIntegratedE2E()
    {
        var appHostSource = File.ReadAllText(FindAppHostSource());
        var uploadBlockStart = appHostSource.IndexOf(
            "var uploadService = WithSharedSecrets(",
            StringComparison.Ordinal);
        var customerBlockStart = appHostSource.IndexOf(
            "var customerService = WithSharedSecrets(",
            StringComparison.Ordinal);

        Assert.True(uploadBlockStart >= 0, "UploadService resource declaration was not found.");
        Assert.True(customerBlockStart > uploadBlockStart, "CustomerService resource declaration was not found after UploadService.");

        var uploadBlock = appHostSource[uploadBlockStart..customerBlockStart];
        Assert.Contains(".WithEnvironment(\"GoogleCloud__Enabled\", \"false\")", uploadBlock, StringComparison.Ordinal);
    }

    /// <summary>
    /// ContactService must stay available for message capture when optional attachment storage is unavailable.
    /// </summary>
    [Fact]
    public void AppHost_ContactService_DoesNotWaitForOptionalUploadService()
    {
        var appHostSource = File.ReadAllText(FindAppHostSource());
        var contactBlockStart = appHostSource.IndexOf(
            "var contactService = WithSharedSecrets(",
            StringComparison.Ordinal);
        var currencyBlockStart = appHostSource.IndexOf(
            "var currencyService = WithSharedSecrets(",
            StringComparison.Ordinal);

        Assert.True(contactBlockStart >= 0, "ContactService resource declaration was not found.");
        Assert.True(currencyBlockStart > contactBlockStart, "CurrencyService resource declaration was not found after ContactService.");

        var contactBlock = appHostSource[contactBlockStart..currencyBlockStart];
        Assert.Contains(".WithReference(uploadService)", contactBlock, StringComparison.Ordinal);
        Assert.DoesNotContain(".WaitFor(uploadService)", contactBlock, StringComparison.Ordinal);
    }

    /// <summary>
    /// PaymentService must receive Omise credentials from Aspire secrets instead of tracked source.
    /// </summary>
    [Fact]
    public void AppHost_PaymentService_ReceivesOmiseSecretConfiguration()
    {
        var appHostSource = File.ReadAllText(FindAppHostSource());
        var paymentBlockStart = appHostSource.IndexOf(
            "var paymentService = WithSharedSecrets(",
            StringComparison.Ordinal);
        var commerceBlockStart = appHostSource.IndexOf(
            "var commerceService = WithSharedSecrets(",
            StringComparison.Ordinal);

        Assert.True(paymentBlockStart >= 0, "PaymentService resource declaration was not found.");
        Assert.True(commerceBlockStart > paymentBlockStart, "CommerceService resource declaration was not found after PaymentService.");

        var paymentBlock = appHostSource[paymentBlockStart..commerceBlockStart];
        Assert.Contains(".WithEnvironment(\"PaymentProviders__Omise__PublicKey\", config.OmisePublicKey)", paymentBlock, StringComparison.Ordinal);
        Assert.Contains(".WithEnvironment(\"PaymentProviders__Omise__SecretKey\", config.OmiseSecretKey)", paymentBlock, StringComparison.Ordinal);
        Assert.Contains(".WithEnvironment(\"PaymentProviders__Omise__WebhookSecret\", config.OmiseWebhookSecret)", paymentBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("PayPal", paymentBlock, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// CommerceService must be available to the Intranet catalog manager.
    /// </summary>
    [Fact]
    public void AppHost_CommerceService_IsRegisteredForCatalogManagement()
    {
        var appHostSource = File.ReadAllText(FindAppHostSource());
        var commerceBlockStart = appHostSource.IndexOf(
            "var commerceService = WithSharedSecrets(",
            StringComparison.Ordinal);
        var pdfBlockStart = appHostSource.IndexOf(
            "var pdfService = WithSharedSecrets(",
            StringComparison.Ordinal);

        Assert.True(commerceBlockStart >= 0, "CommerceService resource declaration was not found.");
        Assert.True(pdfBlockStart > commerceBlockStart, "PdfService resource declaration was not found after CommerceService.");

        var commerceBlock = appHostSource[commerceBlockStart..pdfBlockStart];
        Assert.Contains("builder.AddProject<Projects.Maliev_CommerceService_Api>(\"CommerceService\")", commerceBlock, StringComparison.Ordinal);
        Assert.Contains(".WithReference(databases.Commerce, \"CommerceDbContext\")", commerceBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("Shopify__", commerceBlock, StringComparison.Ordinal);

        var intranetBlockStart = appHostSource.IndexOf(
            "builder.AddProject<Projects.Maliev_Intranet_Bff>(\"IntranetBff\")",
            StringComparison.Ordinal);
        var webBlockStart = appHostSource.IndexOf(
            "builder.AddProject<Projects.Maliev_Web_Bff>(\"WebBff\")",
            StringComparison.Ordinal);

        Assert.True(intranetBlockStart >= 0, "IntranetBff resource declaration was not found.");
        Assert.True(webBlockStart > intranetBlockStart, "WebBff resource declaration was not found after IntranetBff.");

        var intranetBlock = appHostSource[intranetBlockStart..webBlockStart];
        Assert.Contains(".WithReference(commerceService)", intranetBlock, StringComparison.Ordinal);
    }

    /// <summary>
    /// Customer Web launch ports must not collide with other local MALIEV launch profiles.
    /// </summary>
    [Fact]
    public void AppHost_WebBff_LaunchPortsAreUnique()
    {
        var appHostSource = FindAppHostSource();
        var workspaceRoot = Directory.GetParent(Path.GetDirectoryName(appHostSource)!)!.Parent!.FullName;
        var webLaunchSettings = Path.Combine(
            workspaceRoot,
            "Maliev.Web",
            "Maliev.Web.Bff",
            "Properties",
            "launchSettings.json");

        Assert.True(File.Exists(webLaunchSettings), $"WebBff launch settings were not found at {webLaunchSettings}.");

        var webPorts = ReadApplicationPorts(webLaunchSettings).ToHashSet();
        Assert.Contains(5026, webPorts);
        Assert.Contains(7236, webPorts);

        var conflicts = Directory.EnumerateFiles(workspaceRoot, "launchSettings.json", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedPath(path))
            .Where(path => !Path.GetFullPath(path).Equals(Path.GetFullPath(webLaunchSettings), StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => ReadApplicationPorts(path)
                .Where(port => webPorts.Contains(port))
                .Select(port => new PortUse(port, Path.GetRelativePath(workspaceRoot, path))))
            .ToList();

        Assert.Empty(conflicts);
    }

    private static string FindAppHostSource()
    {
        foreach (var startDirectory in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var directory = new DirectoryInfo(startDirectory);
            while (directory is not null)
            {
                var candidates = new[]
                {
                    Path.Combine(directory.FullName, "Maliev.Aspire.AppHost", "AppHost.cs"),
                    Path.Combine(directory.FullName, "Maliev.Aspire", "Maliev.Aspire.AppHost", "AppHost.cs")
                };

                foreach (var candidate in candidates)
                {
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }

                directory = directory.Parent;
            }
        }

        throw new FileNotFoundException("Unable to locate Maliev.Aspire.AppHost/AppHost.cs.");
    }

    private static string FindAppHostProjectFile()
    {
        foreach (var startDirectory in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var directory = new DirectoryInfo(startDirectory);
            while (directory is not null)
            {
                var candidates = new[]
                {
                    Path.Combine(directory.FullName, "Maliev.Aspire.AppHost", "Maliev.Aspire.AppHost.csproj"),
                    Path.Combine(directory.FullName, "Maliev.Aspire", "Maliev.Aspire.AppHost", "Maliev.Aspire.AppHost.csproj")
                };

                foreach (var candidate in candidates)
                {
                    if (File.Exists(candidate))
                    {
                        return candidate;
                    }
                }

                directory = directory.Parent;
            }
        }

        throw new FileNotFoundException("Unable to locate Maliev.Aspire.AppHost/Maliev.Aspire.AppHost.csproj.");
    }

    private static IEnumerable<int> ReadApplicationPorts(string launchSettingsPath)
    {
        using var document = JsonDocument.Parse(File.ReadAllText(launchSettingsPath));
        if (!document.RootElement.TryGetProperty("profiles", out var profiles))
        {
            yield break;
        }

        foreach (var profile in profiles.EnumerateObject())
        {
            if (!profile.Value.TryGetProperty("applicationUrl", out var applicationUrlElement))
            {
                continue;
            }

            var applicationUrl = applicationUrlElement.GetString();
            if (string.IsNullOrWhiteSpace(applicationUrl))
            {
                continue;
            }

            foreach (var url in applicationUrl.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
                {
                    yield return uri.Port;
                }
            }
        }
    }

    private static bool IsGeneratedPath(string path)
    {
        var normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/obj/", StringComparison.OrdinalIgnoreCase)
            || normalized.Contains("/node_modules/", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record PortUse(int Port, string LaunchSettingsPath);
}
