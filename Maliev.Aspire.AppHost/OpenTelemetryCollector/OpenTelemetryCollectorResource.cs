namespace Maliev.Aspire.AppHost.OpenTelemetryCollector;

/// <summary>
/// Represents a resource for hosting an OpenTelemetry Collector instance within a containerized environment.
/// </summary>
/// <param name="name">The name of the resource, which uniquely identifies the OpenTelemetry Collector instance.</param>
public class OpenTelemetryCollectorResource(string name) : ContainerResource(name)
{
    internal const string OtlpGrpcEndpointName = "grpc";
    internal const string OtlpHttpEndpointName = "http";
}
