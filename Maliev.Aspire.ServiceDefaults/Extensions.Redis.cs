using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for adding Redis distributed cache to the application.
/// </summary>
public static class RedisExtensions
{
    /// <summary>
    /// Adds Redis distributed cache with fallback to in-memory cache.
    /// Automatically configures from "redis" connection string if present.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="instanceName">Optional Redis instance name prefix (defaults to service name).</param>
    /// <param name="configureOptions">Optional action to configure Redis options.</param>
    /// <returns>The configured builder.</returns>
    public static IHostApplicationBuilder AddRedisDistributedCache(
        this IHostApplicationBuilder builder,
        string? instanceName = null,
        Action<ConfigurationOptions>? configureOptions = null)
    {
        // Always add in-memory cache as fallback
        builder.Services.AddMemoryCache();

        // Skip Redis configuration in Testing environment
        if (builder.Environment.IsEnvironment("Testing"))
        {
            return builder;
        }

        var redisConnectionString = builder.Configuration.GetConnectionString("redis");
        if (string.IsNullOrEmpty(redisConnectionString))
        {
            return builder;
        }

        try
        {
            var redisOptions = ConfigurationOptions.Parse(redisConnectionString);
            redisOptions.ConnectTimeout = 5000; // 5 second timeout
            redisOptions.SyncTimeout = 5000;
            redisOptions.AbortOnConnectFail = false; // Graceful degradation

            // Apply custom configuration if provided
            configureOptions?.Invoke(redisOptions);

            builder.Services.AddStackExchangeRedisCache(options =>
            {
                options.ConfigurationOptions = redisOptions;
                options.InstanceName = instanceName ?? $"{builder.Environment.ApplicationName}:";
            });

            // Add Redis health check
            builder.Services.AddHealthChecks()
                .AddRedis(redisConnectionString, name: "redis", tags: new[] { "redis", "ready" });
        }
        catch (Exception ex)
        {
            // Log warning but don't fail - fallback to in-memory cache
            var logger = LoggerFactory.Create(b => b.AddConsole())
                .CreateLogger("Redis.Configuration");
            logger.LogWarning(ex, "Failed to configure Redis distributed cache, using in-memory cache");
        }

        return builder;
    }
}
