using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using OpenTelemetry.Logs;
using Maliev.Aspire.ServiceDefaults.IAM;

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
    /// Registers the IAM service client for permission checking and resolution.
    /// This enables services to perform live permission checks and resolve permissions from the central IAM service.
    /// Call this AFTER AddServiceDefaults() if your service needs to make live IAM permission checks (resource-scoped authorization).
    /// </summary>
    /// <param name="builder">The <see cref="IHostApplicationBuilder"/> to configure.</param>
    /// <param name="iamServiceUrl">Optional IAM service URL. If not provided, uses service discovery with "IAMService" name.</param>
    /// <returns>The configured <see cref="IHostApplicationBuilder"/>.</returns>
    public static IHostApplicationBuilder AddIamServiceClient(this IHostApplicationBuilder builder, string? iamServiceUrl = null)
    {
        // Configure the named HttpClient for IAM service
        builder.Services.AddHttpClient("IAMService", client =>
        {
            if (!string.IsNullOrEmpty(iamServiceUrl))
            {
                client.BaseAddress = new Uri(iamServiceUrl);
            }
            // If no URL provided, service discovery will handle it via "IAMService" name
        })
        .AddStandardResilienceHandler(); // Add resilience (retry, circuit breaker, etc.)

        // Register the IAM service client
        builder.Services.AddSingleton<IIamServiceClient, IamServiceClient>();

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
            Predicate = _ => true, // All health checks must pass
            ResponseWriter = async (context, report) =>
            {
                // Return detailed JSON health check response for monitoring and debugging
                context.Response.ContentType = "application/json";
                var result = System.Text.Json.JsonSerializer.Serialize(new
                {
                    status = report.Status.ToString(),
                    checks = report.Entries.ToDictionary(
                        e => e.Key,
                        e => new
                        {
                            status = e.Value.Status.ToString(),
                            description = e.Value.Description ?? string.Empty,
                            duration = e.Value.Duration.TotalMilliseconds,
                            exception = e.Value.Exception?.Message ?? string.Empty,
                            data = e.Value.Data
                        }),
                    totalDuration = report.TotalDuration.TotalMilliseconds
                });
                await context.Response.WriteAsync(result);
            }
        });

        // OpenTelemetry Prometheus metrics endpoint at /{servicePrefix}/metrics
        app.MapPrometheusScrapingEndpoint($"/{servicePrefix}/metrics");

        return app;
    }
}
