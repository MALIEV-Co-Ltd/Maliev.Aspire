namespace Maliev.Aspire.AppHost.OpenTelemetryCollector;

/// <summary>
/// Represents an OpenTelemetry Collector resource for the Aspire AppHost.
/// </summary>
public class OpenTelemetryCollectorResource(string name) : ContainerResource(name)
{
    /// <summary>
    /// The name for the OTLP gRPC endpoint.
    /// </summary>
    internal const string OtlpGrpcEndpointName = "grpc";

    /// <summary>
    /// The name for the OTLP HTTP endpoint.
    /// </summary>
    internal const string OtlpHttpEndpointName = "http";
}
