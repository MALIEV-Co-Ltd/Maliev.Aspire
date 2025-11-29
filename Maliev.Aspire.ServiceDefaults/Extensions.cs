using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

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

        // Add health checks for external dependencies (only if connection strings are configured)
        var postgresConnectionString = builder.Configuration.GetConnectionString("postgres");
        if (!string.IsNullOrEmpty(postgresConnectionString))
        {
            healthChecksBuilder.AddNpgSql(postgresConnectionString);
        }

        var redisConnectionString = builder.Configuration.GetConnectionString("redis");
        if (!string.IsNullOrEmpty(redisConnectionString))
        {
            healthChecksBuilder.AddRedis(redisConnectionString);
        }

        // RabbitMQ health check removed - it blocks during startup and causes timeouts
        // MassTransit already manages RabbitMQ connections and will retry automatically
        // If needed, health checks can be added at the application level instead
        // var rabbitMqConnectionString = builder.Configuration.GetConnectionString("rabbitmq");
        // if (!string.IsNullOrEmpty(rabbitMqConnectionString))
        // {
        //     healthChecksBuilder.AddRabbitMQ(async sp =>
        //     {
        //         var factory = new RabbitMQ.Client.ConnectionFactory
        //         {
        //             Uri = new Uri(rabbitMqConnectionString)
        //         };
        //         return await factory.CreateConnectionAsync();
        //     },
        //     name: "rabbitmq",
        //     tags: new[] { "ready" });
        // }


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
    /// Maps default health and metrics endpoints for the application.
    /// This includes mapping health checks to "/health" and "/alive" endpoints,
    /// and adding a Prometheus scraping endpoint for metrics.
    /// Optionally maps service-specific health check endpoints with a custom prefix.
    /// </summary>
    /// <param name="app">The <see cref="WebApplication"/> to configure.</param>
    /// <param name="servicePrefix">Optional service prefix for custom health endpoints (e.g., "auth" will map "/auth/liveness" and "/auth/readiness").</param>
    /// <returns>The configured <see cref="WebApplication"/>.</returns>
    public static WebApplication MapDefaultEndpoints(this WebApplication app, string? servicePrefix = null)
    {
        // All health checks must pass for app to be considered ready to accept traffic after starting
        app.MapHealthChecks("/health");

        // Only health checks tagged with the "live" tag must pass for app to be considered alive
        app.MapHealthChecks("/alive", new HealthCheckOptions { Predicate = r => r.Tags.Contains("live") });

        // Map service-specific health check endpoints if a prefix is provided
        if (!string.IsNullOrEmpty(servicePrefix))
        {
            // Liveness endpoint - simple check that always returns healthy (for ingress)
            app.MapGet($"/{servicePrefix}/liveness", () => "Healthy").AllowAnonymous();
            
            // Readiness endpoint - checks all dependencies (for ingress)
            app.MapHealthChecks($"/{servicePrefix}/readiness", new HealthCheckOptions
            {
                Predicate = _ => true // All health checks must pass
            });
        }

        // Add the Prometheus scraping endpoint for metrics
        // Wrapped in try-catch to prevent startup failures if Prometheus endpoint fails
        try
        {
            app.MapPrometheusScrapingEndpoint();
        }
        catch (Exception ex)
        {
            var logger = app.Services.GetRequiredService<ILogger<WebApplication>>();
            logger.LogWarning(ex, "Failed to map Prometheus scraping endpoint - continuing without metrics");
        }

        return app;
    }
}
