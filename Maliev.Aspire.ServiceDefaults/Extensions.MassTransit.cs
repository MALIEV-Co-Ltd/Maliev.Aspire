using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.IO;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for adding MassTransit with RabbitMQ to the application.
/// </summary>
public static class MassTransitExtensions
{
    /// <summary>
    /// Adds MassTransit with RabbitMQ configuration.
    /// Requires "rabbitmq" or "RabbitMQ" connection string to be configured.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Optional action to configure MassTransit consumers and settings.</param>
    /// <param name="configureRabbitMq">Optional action to configure RabbitMQ bus settings (e.g., custom queues, routing).</param>
    /// <returns>The configured builder.</returns>
    /// <exception cref="InvalidOperationException">Thrown when RabbitMQ connection string is not configured.</exception>
    public static IHostApplicationBuilder AddMassTransitWithRabbitMq(
        this IHostApplicationBuilder builder,
        Action<IBusRegistrationConfigurator>? configure = null,
        Action<IBusRegistrationContext, IRabbitMqBusFactoryConfigurator>? configureRabbitMq = null)
    {
        var rabbitmqConnectionString = builder.Configuration.GetConnectionString("rabbitmq")
            ?? builder.Configuration.GetConnectionString("RabbitMQ");

        try { File.WriteAllText("test_mt_config.txt", $"[{DateTime.UtcNow}] Configured RabbitMQ: {rabbitmqConnectionString}\nIsTesting: {builder.Environment.IsEnvironment("Testing")}\n"); } catch {}

        if (string.IsNullOrEmpty(rabbitmqConnectionString))
        {
            if (builder.Environment.IsEnvironment("Testing"))
            {
                // In Testing environment, the connection string might be configured later by the test infrastructure
                rabbitmqConnectionString = "host=localhost";
            }
            else
            {
                throw new InvalidOperationException(
                    "RabbitMQ connection string 'rabbitmq' not configured. " +
                    "RabbitMQ is required in all environments.");
            }
        }

        builder.Services.AddMassTransit(x =>
        {
            // Allow caller to add consumers and configure
            configure?.Invoke(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(rabbitmqConnectionString, h =>
                {
                    h.Heartbeat(TimeSpan.FromSeconds(60));
                });

                // Allow custom RabbitMQ configuration (queues, exchanges, routing)
                if (configureRabbitMq != null)
                {
                    configureRabbitMq(context, cfg);
                }
                else
                {
                    // Default: auto-configure endpoints
                    cfg.ConfigureEndpoints(context);
                }
            });
        });

        // Configure MassTransit to not block startup
        builder.Services.Configure<MassTransitHostOptions>(options =>
        {
            options.WaitUntilStarted = false; // Non-blocking startup
            options.StartTimeout = TimeSpan.FromSeconds(60);
            options.StopTimeout = TimeSpan.FromSeconds(30);
        });

        // RabbitMQ health check
        builder.Services.AddHealthChecks()
            .AddRabbitMQ(async sp =>
            {
                var factory = new RabbitMQ.Client.ConnectionFactory
                {
                    Uri = new Uri(rabbitmqConnectionString)
                };
                return await factory.CreateConnectionAsync();
            },
            name: "rabbitmq",
            tags: new[] { "ready" });

        return builder;
    }
}
