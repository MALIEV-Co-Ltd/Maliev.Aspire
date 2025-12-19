using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using StackExchange.Redis;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for adding Redis distributed cache to the application.
/// </summary>
public static class RedisExtensions
{
    /// <summary>
    /// Adds Redis distributed cache to the application.
    /// Requires "redis" connection string to be configured.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="instanceName">Optional Redis instance name prefix (defaults to service name).</param>
    /// <param name="configureOptions">Optional action to configure Redis options.</param>
    /// <returns>The configured builder.</returns>
    /// <exception cref="InvalidOperationException">Thrown when Redis connection string is not configured.</exception>
    public static IHostApplicationBuilder AddRedisDistributedCache(
        this IHostApplicationBuilder builder,
        string? instanceName = null,
        Action<ConfigurationOptions>? configureOptions = null)
    {
        var redisConnectionString = builder.Configuration.GetConnectionString("redis");

        if (string.IsNullOrEmpty(redisConnectionString))
        {
            if (builder.Environment.IsEnvironment("Testing"))
            {
                // Use placeholder connection string - will be replaced by test infrastructure
                redisConnectionString = "localhost:6379";
            }
            else
            {
                throw new InvalidOperationException(
                    "Redis connection string 'redis' not configured. " +
                    "Redis is required in all environments.");
            }
        }

        // Configure Redis options with resilient settings
        var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
        redisOptions.ConnectTimeout = 15000; // 15 second timeout to accommodate container startup
        redisOptions.SyncTimeout = 15000;
        redisOptions.AsyncTimeout = 15000;
        redisOptions.AbortOnConnectFail = false; // Allow connection retries
        redisOptions.ConnectRetry = 10;
        redisOptions.ReconnectRetryPolicy = new ExponentialRetry(1000, 10000); // 1s to 10s exponential backoff

        // Apply custom configuration if provided
        configureOptions?.Invoke(redisOptions);

        builder.Services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnectionString;
            options.InstanceName = instanceName ?? $"{builder.Environment.ApplicationName}:";
            options.ConfigurationOptions = redisOptions;
        });

        // Add Redis health check (skip in Testing as connection may not be valid yet)
        if (!builder.Environment.IsEnvironment("Testing"))
        {
            builder.Services.AddHealthChecks()
                .AddRedis(redisConnectionString, name: "redis", tags: ["ready"]);
        }


        return builder;
    }

    /// <summary>
    /// Adds Redis IConnectionMultiplexer to the application.
    /// Requires "redis" connection string to be configured.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="connectionName">Connection string name (defaults to "redis").</param>
    /// <param name="configureOptions">Optional action to configure Redis options.</param>
    /// <returns>The configured builder.</returns>
    public static IHostApplicationBuilder AddRedisConnectionMultiplexer(
        this IHostApplicationBuilder builder,
        string connectionName = "redis",
        Action<ConfigurationOptions>? configureOptions = null)
    {
        var redisConnectionString = builder.Configuration.GetConnectionString(connectionName);

        if (string.IsNullOrEmpty(redisConnectionString))
        {
            throw new InvalidOperationException(
                $"Redis connection string '{connectionName}' not configured. " +
                "Redis is required in all environments.");
        }

        // Configure Redis options with resilient settings
        var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
        redisOptions.ConnectTimeout = 15000; // 15 second timeout to accommodate container startup
        redisOptions.SyncTimeout = 15000;
        redisOptions.AsyncTimeout = 15000;
        redisOptions.AbortOnConnectFail = false; // Allow connection retries
        redisOptions.ConnectRetry = 10;
        redisOptions.ReconnectRetryPolicy = new ExponentialRetry(1000, 10000); // 1s to 10s exponential backoff

        // Apply custom configuration if provided
        configureOptions?.Invoke(redisOptions);

        // Register IConnectionMultiplexer as a singleton
        builder.Services.AddSingleton<IConnectionMultiplexer>(sp => 
            ConnectionMultiplexer.Connect(redisOptions));

        return builder;
    }
}