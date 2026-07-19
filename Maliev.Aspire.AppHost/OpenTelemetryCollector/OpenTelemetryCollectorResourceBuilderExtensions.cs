using Aspire.Hosting.ApplicationModel;
using Maliev.Aspire.AppHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Maliev.Aspire.AppHost.OpenTelemetryCollector;

/// <summary>
/// Extension methods for adding OpenTelemetry Collector resources.
/// </summary>
public static class OpenTelemetryCollectorResourceBuilderExtensions
{
    private const string OtelExporterOtlpEndpoint = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private const string DashboardOtlpUrlVariableName = "ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL";
    private const string DashboardOtlpApiKeyVariableName = "AppHost:OtlpApiKey";
    private const string DashboardOtlpUrlDefaultValue = "http://host.docker.internal:18889";
    private const string OTelCollectorImageName = "ghcr.io/open-telemetry/opentelemetry-collector-releases/opentelemetry-collector-contrib";
    private const string OTelCollectorImageTag = "0.114.0";

    /// <summary>
    /// Adds an OpenTelemetry Collector resource to the distributed application.
    /// </summary>
    /// <param name="builder">The distributed application builder.</param>
    /// <param name="name">The name of the resource.</param>
    /// <param name="configFileLocation">The location of the collector configuration file.</param>
    /// <returns>A resource builder for the OpenTelemetry Collector.</returns>
    public static IResourceBuilder<OpenTelemetryCollectorResource> AddOpenTelemetryCollector(this IDistributedApplicationBuilder builder, string name, string configFileLocation)
    {
        var url = builder.Configuration[DashboardOtlpUrlVariableName] ?? DashboardOtlpUrlDefaultValue;
        var isHttpsEnabled = url.StartsWith("https", StringComparison.OrdinalIgnoreCase);

        var dashboardOtlpEndpoint = new HostUrl(url);
        var configFilePath = AppHostPathResolver.ResolveRequiredFilePath(configFileLocation);

        var collectorResource = new OpenTelemetryCollectorResource(name);
        var resourceBuilder = builder.AddResource(collectorResource)
            .WithImage(OTelCollectorImageName, OTelCollectorImageTag)
            .WithEndpoint(targetPort: 4317, name: OpenTelemetryCollectorResource.OtlpGrpcEndpointName, scheme: "http")
            .WithEndpoint(targetPort: 4318, name: OpenTelemetryCollectorResource.OtlpHttpEndpointName, scheme: "http")
            .WithUrlForEndpoint(OpenTelemetryCollectorResource.OtlpGrpcEndpointName, u => u.DisplayLocation = UrlDisplayLocation.DetailsOnly)
            .WithUrlForEndpoint(OpenTelemetryCollectorResource.OtlpHttpEndpointName, u => u.DisplayLocation = UrlDisplayLocation.DetailsOnly)
            .WithContainerFiles("/etc/otelcol-contrib", [
                new ContainerFile
                {
                    Name = "config.yaml",
                    Contents = File.ReadAllText(configFilePath)
                }
            ])
            .WithEnvironment("ASPIRE_ENDPOINT", $"{dashboardOtlpEndpoint}")
            .WithEnvironment("ASPIRE_API_KEY", builder.Configuration[DashboardOtlpApiKeyVariableName])
            .WithEnvironment("ASPIRE_INSECURE", isHttpsEnabled ? "false" : "true");

        var otlpEndpoint = collectorResource.GetEndpoint(OpenTelemetryCollectorResource.OtlpGrpcEndpointName);

        builder.Eventing.Subscribe<BeforeResourceStartedEvent>((e, ct) =>
        {
            // Skip the collector itself to avoid modifying its own annotations while they are being enumerated
            if (ReferenceEquals(e.Resource, collectorResource))
            {
                return Task.CompletedTask;
            }

            // Update the starting resource to forward telemetry to the collector.
            e.Resource.Annotations.Add(new EnvironmentCallbackAnnotation(context =>
            {
                if (context.EnvironmentVariables.ContainsKey(OtelExporterOtlpEndpoint))
                {
                    context.EnvironmentVariables[OtelExporterOtlpEndpoint] = otlpEndpoint;
                }
            }));

            return Task.CompletedTask;
        });

        // Always pass the config file argument for the collector to start properly
        resourceBuilder.WithArgs(@"--config=/etc/otelcol-contrib/config.yaml");

        return resourceBuilder;
    }

}
