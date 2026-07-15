using System.Text.Json;

namespace Maliev.Aspire.Tests;

/// <summary>
/// Tests for AppHost service discovery references used by cross-service workflows.
/// </summary>
public sealed class AppHostReferenceTests
{
    /// <summary>
    /// The local live-permission credential must be raw only in IntranetBff and hashed only in IAMService.
    /// </summary>
    [Fact]
    public void AppHost_IamLiveCheckCredential_IsIsolatedToAuthorizedResources()
    {
        var appHostSource = File.ReadAllText(FindAppHostSource());

        Assert.Contains("RandomNumberGenerator.GetBytes(32)", appHostSource, StringComparison.Ordinal);
        Assert.Contains("Convert.ToBase64String(", appHostSource, StringComparison.Ordinal);
        Assert.Contains("SHA256.HashData", appHostSource, StringComparison.Ordinal);
        Assert.Contains(
            ".WithEnvironment(\"IAM__LivePermissionChecks__CredentialHashes__IntranetBff\", config.IamLiveCheckCredentialHash)",
            appHostSource,
            StringComparison.Ordinal);
        Assert.Contains(
            ".WithEnvironment(\"IAM__LivePermissionChecks__Credential\", config.IntranetBffIamLiveCheckCredential)",
            appHostSource,
            StringComparison.Ordinal);

        Assert.Equal(1, CountOccurrences(appHostSource, "IAM__LivePermissionChecks__Credential\""));
        Assert.Equal(1, CountOccurrences(appHostSource, "IAM__LivePermissionChecks__CredentialHashes__IntranetBff\""));
    }

    /// <summary>
    /// QuotationService must be able to resolve customer data and PDF generation during formal quote creation.
    /// </summary>
    [Fact]
    public void AppHost_QuotationService_ReferencesCustomerService()
    {
        var appHostSource = File.ReadAllText(FindAppHostSource());
        var quotationBlockStart = appHostSource.IndexOf(
            "var quotationService = WithSharedSecrets(",
            StringComparison.Ordinal);
        var pdfReferenceStart = appHostSource.IndexOf(
            "quotationService = quotationService",
            StringComparison.Ordinal);

        Assert.True(quotationBlockStart >= 0, "QuotationService resource declaration was not found.");
        Assert.True(pdfReferenceStart > quotationBlockStart, "QuotationService PdfService reference block was not found after QuotationService.");

        var quotationBlock = appHostSource[quotationBlockStart..pdfReferenceStart];
        Assert.Contains(".WithReference(customerService)", quotationBlock, StringComparison.Ordinal);
        var pdfReferenceBlock = appHostSource[pdfReferenceStart..];
        Assert.Contains(".WithReference(pdfService)", pdfReferenceBlock, StringComparison.Ordinal);
        Assert.Contains(".WaitFor(pdfService)", pdfReferenceBlock, StringComparison.Ordinal);
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
        // The Intranet BFF is declared at the top of the file and wired at the bottom; the
        // geometry-endpoint reference is the first chained 'intranetBff' wiring after the
        // GeometryService declaration block.
        var bffReferenceStart = appHostSource.IndexOf(
            "        intranetBff",
            geometryBlockStart,
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
        // IntranetBff is declared at the top of the file and wired near the bottom; slice the
        // wiring block (DbContext reference → dashboard HTTP command) to inspect its references.
        var intranetWiringStart = appHostSource.IndexOf(
            ".WithReference(databases.Intranet, \"IntranetDbContext\")",
            StringComparison.Ordinal);
        var intranetWiringEnd = appHostSource.IndexOf(
            ".WithHttpCommand(",
            intranetWiringStart,
            StringComparison.Ordinal);

        Assert.True(customerBlockStart >= 0, "CustomerService resource declaration was not found.");
        Assert.True(employeeBlockStart > customerBlockStart, "EmployeeService resource declaration was not found after CustomerService.");
        Assert.True(intranetWiringStart >= 0, "IntranetBff service wiring block was not found.");
        Assert.True(intranetWiringEnd > intranetWiringStart, "IntranetBff service wiring block end was not found.");

        var customerBlock = appHostSource[customerBlockStart..employeeBlockStart];
        var intranetBlock = appHostSource[intranetWiringStart..intranetWiringEnd];
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
    /// AuthService must receive the shared browser GIS client as the exact audience for each
    /// application/caller binding accepted by its employee and customer Google exchanges.
    /// </summary>
    [Fact]
    public void AppHost_AuthService_MapsSharedGoogleIdentityClientToExactApplicationAudiences()
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
        Assert.Contains(
            ".WithEnvironment(\"GoogleIdentity__Employee__Audiences__intranet__0\", config.GoogleClientId)",
            authBlock,
            StringComparison.Ordinal);
        Assert.Contains(
            ".WithEnvironment(\"GoogleIdentity__Customer__Audiences__web__0\", config.GoogleClientId)",
            authBlock,
            StringComparison.Ordinal);
        Assert.Contains(
            ".WithEnvironment(\"GoogleIdentity__Customer__Audiences__quote-engine__0\", config.GoogleClientId)",
            authBlock,
            StringComparison.Ordinal);
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
        var quoteEngineResourceBlockStart = appHostSource.IndexOf(
            "builder.AddProject<Projects.Maliev_QuoteEngine_Bff>(\"QuoteEngineBff\")",
            StringComparison.Ordinal);
        var webResourceBlockStart = appHostSource.IndexOf(
            "builder.AddProject<Projects.Maliev_Web_Bff>(\"WebBff\")",
            StringComparison.Ordinal);
        var webReferenceBlockStart = appHostSource.IndexOf(
            "        webBff",
            webResourceBlockStart,
            StringComparison.Ordinal);
        var quoteEngineReferenceBlockStart = appHostSource.LastIndexOf(
            "        quoteEngineBff",
            webReferenceBlockStart,
            StringComparison.Ordinal);

        Assert.True(quoteEngineResourceBlockStart >= 0, "QuoteEngineBff resource declaration was not found.");
        Assert.True(webResourceBlockStart > quoteEngineResourceBlockStart, "WebBff resource declaration was not found after QuoteEngineBff.");
        Assert.True(webReferenceBlockStart > webResourceBlockStart, "WebBff service reference block was not found.");
        Assert.True(quoteEngineReferenceBlockStart > quoteEngineResourceBlockStart, "QuoteEngineBff service reference block was not found.");

        var quoteEngineResourceBlock = appHostSource[quoteEngineResourceBlockStart..webResourceBlockStart];
        var quoteEngineReferenceBlock = appHostSource[quoteEngineReferenceBlockStart..webReferenceBlockStart];
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
            "invoiceService",
            "pdfService",
            "orderService",
            "paymentService",
            "deliveryService",
            "chatbotService",
            "receiptService",
            "searchService",
            "currencyService"
        })
        {
            Assert.Contains($".WithReference({dependency})", quoteEngineReferenceBlock, StringComparison.Ordinal);
        }

        Assert.Contains(".WaitFor(infrastructure.RabbitMQ)", quoteEngineReferenceBlock, StringComparison.Ordinal);
        Assert.Contains(".WithUrlForEndpoint(\"http\", u => u.DisplayText = \"Quote Engine (HTTP)\")", quoteEngineResourceBlock, StringComparison.Ordinal);
        Assert.Contains(".WithUrlForEndpoint(\"https\", u => u.DisplayText = \"Quote Engine (HTTPS)\")", quoteEngineResourceBlock, StringComparison.Ordinal);
        Assert.Contains(".WithTestingSafeHttpHealthCheck(\"/quote/aspire-liveness\")", quoteEngineResourceBlock, StringComparison.Ordinal);
    }

    /// <summary>
    /// ChatbotService must resolve the QuoteEngine BFF so the Quote Agent can execute
    /// tool callbacks (/quote/v1/agent/tools/*); without this reference the named
    /// "QuoteEngineBff" client falls back to the unresolvable literal service host.
    /// </summary>
    [Fact]
    public void AppHost_ChatbotService_ReferencesQuoteEngineBff()
    {
        var appHostSource = File.ReadAllText(FindAppHostSource());
        var chatbotBlockStart = appHostSource.IndexOf(
            "var chatbotService = WithSharedSecrets(",
            StringComparison.Ordinal);
        var projectBlockStart = appHostSource.IndexOf(
            "var projectService = WithSharedSecrets(",
            StringComparison.Ordinal);

        Assert.True(chatbotBlockStart >= 0, "ChatbotService resource declaration was not found.");
        Assert.True(projectBlockStart > chatbotBlockStart, "ProjectService resource declaration was not found after ChatbotService.");

        var chatbotBlock = appHostSource[chatbotBlockStart..projectBlockStart];
        Assert.Contains(".WithReference(quoteEngineBff)", chatbotBlock, StringComparison.Ordinal);
        // No WaitFor on the reverse edge: quoteEngineBff already references chatbotService,
        // and a WaitFor here would create a startup-ordering cycle.
        Assert.DoesNotContain(".WaitFor(quoteEngineBff)", chatbotBlock, StringComparison.Ordinal);
    }

    /// <summary>
    /// Aspire owns local Quote Agent thinking callback wiring because it knows both
    /// the QuoteEngine public endpoint and ChatbotService callback allow-list.
    /// </summary>
    [Fact]
    public void AppHost_QuoteAgentThinkingCallbacks_WiresLocalCallbackBaseAndAllowedOrigin()
    {
        var appHostSource = File.ReadAllText(FindAppHostSource());

        Assert.Contains(
            ".WithEnvironment(\"Chatbot__AllowedThinkingCallbackOrigins__0\", quoteEngineBff.GetEndpoint(\"https\"))",
            appHostSource,
            StringComparison.Ordinal);
        Assert.Contains(
            ".WithEnvironment(\"QuoteAgent__EnableThinkingCallbacks\", \"true\")",
            appHostSource,
            StringComparison.Ordinal);
        Assert.Contains(
            ".WithEnvironment(\"QuoteAgent__ThinkingCallbackBaseUrl\", quoteEngineBff.GetEndpoint(\"https\"))",
            appHostSource,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// QuoteEngine BFF must receive GeometryService endpoint discovery for CAD analysis flows.
    /// </summary>
    [Fact]
    public void AppHost_QuoteEngineBff_ReferencesGeometryServiceEndpoint()
    {
        var appHostSource = File.ReadAllText(FindAppHostSource());

        Assert.Contains("quoteEngineBff.WithReference(geometryService.GetEndpoint(\"http\"));", appHostSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// Customer Web BFF must be registered with the downstream services used by its BFF clients.
    /// </summary>
    [Fact]
    public void AppHost_WebBff_ReferencesCustomerFacingServices()
    {
        var appHostSource = File.ReadAllText(FindAppHostSource());
        // WebBff is declared at the top of the file and wired near the bottom; slice from the
        // start of its wiring block through the end of ConfigureServices (the WithSharedSecrets
        // helper method) so both the service references and the applied environment are covered.
        var webWiringStart = appHostSource.IndexOf(
            "        webBff",
            StringComparison.Ordinal);
        var webWiringEnd = appHostSource.IndexOf(
            "private static IResourceBuilder<ProjectResource> WithSharedSecrets(",
            webWiringStart,
            StringComparison.Ordinal);

        Assert.True(webWiringStart >= 0, "WebBff service wiring block was not found.");
        Assert.True(webWiringEnd > webWiringStart, "WebBff service wiring block end was not found.");

        var webBlock = appHostSource[webWiringStart..webWiringEnd];
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
        Assert.Contains(".WithEnvironment(\"Authentication__Google__ClientId\", config.GoogleClientId)", webBlock, StringComparison.Ordinal);
        Assert.DoesNotContain("Authentication__Google__ClientSecret", appHostSource, StringComparison.Ordinal);
        // The /web/aspire-liveness health check lives on the top-of-file WebBff declaration.
        Assert.Contains(".WithTestingSafeHttpHealthCheck(\"/web/aspire-liveness\")", appHostSource, StringComparison.Ordinal);
    }

    /// <summary>
    /// Maliev.Web, Maliev.Intranet, and Maliev.QuoteEngine share a single Google OAuth client
    /// (the same client also backs QuoteEngine Google Drive access). The earlier dedicated
    /// customer-facing Web client (WEB-006) was dropped, so no Web-specific OAuth scaffolding
    /// should remain in the AppHost.
    /// </summary>
    [Fact]
    public void AppHost_WebBff_UsesSharedGoogleOAuthClient()
    {
        var appHostSource = File.ReadAllText(FindAppHostSource());

        Assert.Contains("\"GoogleClientId\", \"Authentication:Google:ClientId\"", appHostSource, StringComparison.Ordinal);
        Assert.DoesNotContain("Authentication:Google:Web", appHostSource, StringComparison.Ordinal);
        Assert.DoesNotContain("WebGoogleClientId", appHostSource, StringComparison.Ordinal);
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

        // IntranetBff is declared at the top of the file and wired near the bottom; slice the
        // wiring block (DbContext reference → dashboard HTTP command) to inspect its references.
        var intranetWiringStart = appHostSource.IndexOf(
            ".WithReference(databases.Intranet, \"IntranetDbContext\")",
            StringComparison.Ordinal);
        var intranetWiringEnd = appHostSource.IndexOf(
            ".WithHttpCommand(",
            intranetWiringStart,
            StringComparison.Ordinal);

        Assert.True(intranetWiringStart >= 0, "IntranetBff service wiring block was not found.");
        Assert.True(intranetWiringEnd > intranetWiringStart, "IntranetBff service wiring block end was not found.");

        var intranetBlock = appHostSource[intranetWiringStart..intranetWiringEnd];
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

    private static int CountOccurrences(string source, string value)
    {
        var count = 0;
        var startIndex = 0;
        while ((startIndex = source.IndexOf(value, startIndex, StringComparison.Ordinal)) >= 0)
        {
            count++;
            startIndex += value.Length;
        }

        return count;
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
