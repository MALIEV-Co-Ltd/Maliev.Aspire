using MassTransit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for adding MassTransit with RabbitMQ to the application.
/// </summary>
public static class MassTransitExtensions
{
    /// <summary>
    /// Adds MassTransit with RabbitMQ configuration.
    /// Automatically configures from "rabbitmq" connection string if present.
    /// Skips configuration in Testing environment.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Optional action to configure MassTransit consumers and settings.</param>
    /// <returns>The configured builder.</returns>
    public static IHostApplicationBuilder AddMassTransitWithRabbitMq(
        this IHostApplicationBuilder builder,
        Action<IBusRegistrationConfigurator>? configure = null)
    {
        // Skip MassTransit in Testing environment
        if (builder.Environment.IsEnvironment("Testing"))
        {
            return builder;
        }

        var rabbitmqConnectionString = builder.Configuration.GetConnectionString("rabbitmq");
        if (string.IsNullOrEmpty(rabbitmqConnectionString))
        {
            return builder;
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

                cfg.ConfigureEndpoints(context);
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
