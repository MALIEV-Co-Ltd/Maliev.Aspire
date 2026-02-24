using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Maliev.Aspire.AppHost.OpenTelemetryCollector;

/// <summary>
/// Provides extension methods for configuring and adding an OpenTelemetry Collector resource to a distributed
/// application builder.
/// </summary>
/// <remarks>This class enables integration of the OpenTelemetry Collector into distributed applications, allowing
/// for centralized telemetry data collection and processing. It manages configuration settings such as the collector's
/// image name, version, endpoints, and environment variables required for operation. Use these extensions to streamline
/// the setup of telemetry infrastructure within Aspire-based applications.</remarks>
public static class OpenTelemetryCollectorResourceBuilderExtensions
{
    private const string OtelExporterOtlpEndpoint = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private const string DashboardOtlpUrlVariableName = "ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL";
    private const string DashboardOtlpApiKeyVariableName = "AppHost:OtlpApiKey";
    private const string DashboardOtlpUrlDefaultValue = "http://host.docker.internal:18889";
    private const string OTelCollectorImageName = "ghcr.io/open-telemetry/opentelemetry-collector-releases/opentelemetry-collector-contrib";
    private const string OTelCollectorImageTag = "0.114.0";

    /// <summary>
    /// Adds an OpenTelemetry collector resource to the distributed application builder, configuring it with the
    /// specified name and settings from the provided configuration file.
    /// </summary>
    /// <remarks>This method configures the OpenTelemetry collector with endpoints and environment variables
    /// to enable telemetry forwarding. It also ensures the collector is properly initialized with the specified
    /// configuration file and updates resource annotations to forward telemetry to the collector's endpoint.</remarks>
    /// <param name="builder">The distributed application builder to which the OpenTelemetry collector resource will be added.</param>
    /// <param name="name">The name to assign to the OpenTelemetry collector resource.</param>
    /// <param name="configFileLocation">The file path to the OpenTelemetry collector configuration file. Must point to a valid YAML configuration.</param>
    /// <returns>An instance of <see cref="IResourceBuilder{T}"/> of <see cref="OpenTelemetryCollectorResource"/> that can be used to further configure the
    /// OpenTelemetry collector resource.</returns>
    public static IResourceBuilder<OpenTelemetryCollectorResource> AddOpenTelemetryCollector(this IDistributedApplicationBuilder builder, string name, string configFileLocation)
    {
        var url = builder.Configuration[DashboardOtlpUrlVariableName] ?? DashboardOtlpUrlDefaultValue;
        var isHttpsEnabled = url.StartsWith("https", StringComparison.OrdinalIgnoreCase);

        var dashboardOtlpEndpoint = new HostUrl(url);

        var collectorResource = new OpenTelemetryCollectorResource(name);
        var resourceBuilder = builder.AddResource(collectorResource)
            .WithImage(OTelCollectorImageName, OTelCollectorImageTag)
            .WithEndpoint(targetPort: 4317, name: OpenTelemetryCollectorResource.OtlpGrpcEndpointName, scheme: "http")
            .WithEndpoint(targetPort: 4318, name: OpenTelemetryCollectorResource.OtlpHttpEndpointName, scheme: "http")
            .WithUrlForEndpoint(OpenTelemetryCollectorResource.OtlpGrpcEndpointName, u => u.DisplayLocation = UrlDisplayLocation.DetailsOnly)
            .WithUrlForEndpoint(OpenTelemetryCollectorResource.OtlpHttpEndpointName, u => u.DisplayLocation = UrlDisplayLocation.DetailsOnly)
            .WithBindMount(configFileLocation, "/etc/otelcol-contrib/config.yaml")
            .WithEnvironment("ASPIRE_ENDPOINT", $"{dashboardOtlpEndpoint}")
            .WithEnvironment("ASPIRE_API_KEY", builder.Configuration[DashboardOtlpApiKeyVariableName])
            .WithEnvironment("ASPIRE_INSECURE", isHttpsEnabled ? "false" : "true");

        builder.Eventing.Subscribe<BeforeResourceStartedEvent>((e, ct) =>
        {
            var endpoint = collectorResource.GetEndpoint(OpenTelemetryCollectorResource.OtlpGrpcEndpointName);
            if (!endpoint.Exists)
            {
                return Task.CompletedTask;
            }

            // Update the starting resource to forward telemetry to the collector.
            e.Resource.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
            {
                if (context.EnvironmentVariables.ContainsKey(OtelExporterOtlpEndpoint))
                {
                    context.EnvironmentVariables[OtelExporterOtlpEndpoint] = endpoint;
                }
            }));

            return Task.CompletedTask;
        });

        // Always pass the config file argument for the collector to start properly
        resourceBuilder.WithArgs(@"--config=/etc/otelcol-contrib/config.yaml");

        return resourceBuilder;
    }
}
