using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Threading.RateLimiting;

namespace Microsoft.Extensions.Hosting;

public static class RateLimitingExtensions
{
    /// <summary>
    /// Adds standard rate limiting with configurable options.
    /// Uses fixed window rate limiter by default.
    /// </summary>
    public static IHostApplicationBuilder AddStandardRateLimiting(
        this IHostApplicationBuilder builder,
        Action<RateLimiterOptions>? configure = null)
    {
        var options = new RateLimiterOptions
        {
            PermitLimit = 100,
            WindowMinutes = 1,
            QueueLimit = 0,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst
        };

        configure?.Invoke(options);

        builder.Services.AddRateLimiter(rateLimiterOptions =>
        {
            rateLimiterOptions.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            if (options.UseGlobalLimiter)
            {
                rateLimiterOptions.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                {
                    var partitionKey = options.PartitionKeySelector?.Invoke(context) ??
                                       context.User.Identity?.Name ??
                                       context.Request.Headers.Host.ToString();

                    return RateLimitPartition.GetFixedWindowLimiter(
                        partitionKey: partitionKey,
                        factory: _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = options.PermitLimit,
                            Window = TimeSpan.FromMinutes(options.WindowMinutes),
                            QueueProcessingOrder = options.QueueProcessingOrder,
                            QueueLimit = options.QueueLimit
                        });
                });
            }

            // Named policy for API endpoints
            rateLimiterOptions.AddFixedWindowLimiter("api", limiterOptions =>
            {
                limiterOptions.PermitLimit = options.PermitLimit;
                limiterOptions.Window = TimeSpan.FromMinutes(options.WindowMinutes);
                limiterOptions.QueueProcessingOrder = options.QueueProcessingOrder;
                limiterOptions.QueueLimit = options.QueueLimit;
            });

            // Stricter policy for write operations
            rateLimiterOptions.AddFixedWindowLimiter("write", limiterOptions =>
            {
                limiterOptions.PermitLimit = options.PermitLimit / 2;
                limiterOptions.Window = TimeSpan.FromMinutes(options.WindowMinutes);
                limiterOptions.QueueProcessingOrder = options.QueueProcessingOrder;
                limiterOptions.QueueLimit = options.QueueLimit;
            });

            // More lenient policy for read operations
            rateLimiterOptions.AddFixedWindowLimiter("read", limiterOptions =>
            {
                limiterOptions.PermitLimit = options.PermitLimit * 2;
                limiterOptions.Window = TimeSpan.FromMinutes(options.WindowMinutes);
                limiterOptions.QueueProcessingOrder = options.QueueProcessingOrder;
                limiterOptions.QueueLimit = options.QueueLimit;
            });
        });

        return builder;
    }
}

public class RateLimiterOptions
{
    /// <summary>
    /// Use global rate limiter (applies to all requests).
    /// Default: true
    /// </summary>
    public bool UseGlobalLimiter { get; set; } = true;

    /// <summary>
    /// Number of requests allowed per window.
    /// Default: 100
    /// </summary>
    public int PermitLimit { get; set; } = 100;

    /// <summary>
    /// Time window in minutes.
    /// Default: 1 minute
    /// </summary>
    public int WindowMinutes { get; set; } = 1;

    /// <summary>
    /// Maximum queue size for waiting requests.
    /// Default: 0 (no queue)
    /// </summary>
    public int QueueLimit { get; set; } = 0;

    /// <summary>
    /// Queue processing order.
    /// Default: OldestFirst
    /// </summary>
    public QueueProcessingOrder QueueProcessingOrder { get; set; } = QueueProcessingOrder.OldestFirst;

    /// <summary>
    /// Custom partition key selector.
    /// Default: User name or Host header
    /// </summary>
    public Func<HttpContext, string>? PartitionKeySelector { get; set; }
}
