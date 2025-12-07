using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Provides extension methods for configuring service defaults in a distributed application.
/// This class includes methods for setting up OpenTelemetry, health checks, service discovery, and resilience.
/// </summary>
public static class Extensions
{
    /// <summary>
    /// Adds default service configurations to the application builder.
    /// This includes setting up OpenTelemetry for logging, metrics, and tracing,
    /// configuring health checks for the application and its dependencies,
    /// and enabling service discovery and resilience for HTTP clients.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/> to configure.</param>
    /// <returns>The configured <see cref="IHostApplicationBuilder"/>.</returns>
    public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
    {
        // --- OpenTelemetry ---
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;

            if (!string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
            {
                logging.AddOtlpExporter();
            }
        });

        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddPrometheusExporter(); // Export metrics for Prometheus

                if (useOtlpExporter)
                {
                    metrics.AddOtlpExporter();
                }
            })
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation()
                       .AddHttpClientInstrumentation();

                if (useOtlpExporter)
                {
                    tracing.AddOtlpExporter();
                }
            });


        // --- Health Checks ---
        var healthChecksBuilder = builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        // --- Service Discovery and Resilience ---
        builder.Services.AddServiceDiscovery();
        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        return builder;
    }

    /// <summary>
    /// Adds service-specific meters to OpenTelemetry metrics collection.
    /// Call this AFTER AddServiceDefaults() to register custom business metrics meters.
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/> to configure.</param>
    /// <param name="meterNames">Names of meters to add (e.g., "customer-service", "payment-service").</param>
    /// <returns>The configured <see cref="IHostApplicationBuilder"/>.</returns>
    public static IHostApplicationBuilder AddServiceMeters(this IHostApplicationBuilder builder, params string[] meterNames)
    {
        builder.Services.ConfigureOpenTelemetryMeterProvider(metrics =>
        {
            foreach (var meterName in meterNames)
            {
                metrics.AddMeter(meterName);
            }
        });

        return builder;
    }

    /// <summary>
    /// Maps default health and metrics endpoints for the application.
    /// This includes mapping health checks to "/health" and "/alive" endpoints,
    /// and adding an OpenTelemetry Prometheus scraping endpoint at /{servicePrefix}/metrics.
    /// Also maps service-specific health check endpoints at /{servicePrefix}/liveness and /{servicePrefix}/readiness.
    /// </summary>
    /// <param name="app">The <see cref="WebApplication"/> to configure.</param>
    /// <param name="servicePrefix">Required service prefix for health and metrics endpoints (e.g., "auth" will map "/auth/liveness", "/auth/readiness", "/auth/metrics").</param>
    /// <returns>The configured <see cref="WebApplication"/>.</returns>
    public static WebApplication MapDefaultEndpoints(this WebApplication app, string servicePrefix)
    {
        if (string.IsNullOrWhiteSpace(servicePrefix))
        {
            throw new ArgumentException("Service prefix is required for MapDefaultEndpoints", nameof(servicePrefix));
        }

        // Liveness endpoint - simple check that always returns healthy (for ingress)
        app.MapGet($"/{servicePrefix}/liveness", () => "Healthy").AllowAnonymous();

        // Readiness endpoint - checks all dependencies (for ingress)
        app.MapHealthChecks($"/{servicePrefix}/readiness", new HealthCheckOptions
        {
            Predicate = _ => true // All health checks must pass
        });

        // OpenTelemetry Prometheus metrics endpoint at /{servicePrefix}/metrics
        app.MapPrometheusScrapingEndpoint($"/{servicePrefix}/metrics");

        return app;
    }
}
