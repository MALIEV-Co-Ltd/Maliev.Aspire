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

    private static string FindAppHostSource()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            var candidate = Path.Combine(directory.FullName, "Maliev.Aspire.AppHost", "AppHost.cs");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            directory = directory.Parent;
        }

        throw new FileNotFoundException("Unable to locate Maliev.Aspire.AppHost/AppHost.cs.");
    }
}
