using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Maliev.Aspire.AppHost.OpenTelemetryCollector;

public static class OpenTelemetryCollectorResourceBuilderExtensions
{
    private const string OtelExporterOtlpEndpoint = "OTEL_EXPORTER_OTLP_ENDPOINT";
    private const string DashboardOtlpUrlVariableName = "ASPIRE_DASHBOARD_OTLP_ENDPOINT_URL";
    private const string DashboardOtlpApiKeyVariableName = "AppHost:OtlpApiKey";
    private const string DashboardOtlpUrlDefaultValue = "http://host.docker.internal:18889";
    private const string OTelCollectorImageName = "ghcr.io/open-telemetry/opentelemetry-collector-releases/opentelemetry-collector-contrib";
    private const string OTelCollectorImageTag = "0.114.0";

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
