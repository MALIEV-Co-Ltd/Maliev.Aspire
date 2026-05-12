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
    /// GeometryService must not start OTLP export before the collector is ready.
    /// </summary>
    [Fact]
    public void AppHost_GeometryService_WaitsForOpenTelemetryCollector()
    {
        var appHostSource = File.ReadAllText(FindAppHostSource());
        var geometryBlockStart = appHostSource.IndexOf(
            "var geometryService = builder.AddDockerfile(",
            StringComparison.Ordinal);
        var intranetBffReferenceCommentStart = appHostSource.IndexOf(
            "// Wire GeometryService into IntranetBff for service discovery.",
            StringComparison.Ordinal);

        Assert.True(geometryBlockStart >= 0, "GeometryService resource declaration was not found.");
        Assert.True(intranetBffReferenceCommentStart > geometryBlockStart, "GeometryService reference block was not found after GeometryService.");

        var geometryBlock = appHostSource[geometryBlockStart..intranetBffReferenceCommentStart];
        Assert.Contains(".WithEnvironment(\"OTEL_EXPORTER_OTLP_ENDPOINT\", otelCollector.GetEndpoint(\"grpc\"))", geometryBlock, StringComparison.Ordinal);
        Assert.Contains(".WaitFor(otelCollector)", geometryBlock, StringComparison.Ordinal);
    }

    /// <summary>
    /// Aspire should expose explicit seed commands for the local test administrator login path.
    /// </summary>
    [Fact]
    public void AppHost_LocalTestAdmin_RegistersEmployeeAndIamSeeders()
    {
        var appHostSource = File.ReadAllText(FindAppHostSource());

        Assert.Contains(".SeedDatabase<EmployeeDatabaseSeeder>(databases.Employee", appHostSource, StringComparison.Ordinal);
        Assert.Contains(".SeedDatabase<IAMDatabaseSeeder>(databases.IAM", appHostSource, StringComparison.Ordinal);
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
            "iamService",
            "customerService",
            "deliveryService",
            "materialService",
            "orderService",
            "paymentService",
            "pricingService",
            "uploadService",
            "commerceService"
        })
        {
            Assert.Contains($".WithReference({dependency})", webBlock, StringComparison.Ordinal);
        }

        Assert.Contains(".WithHttpHealthCheck(\"/web/aspire-liveness\")", webBlock, StringComparison.Ordinal);
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
