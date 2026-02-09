using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.RateLimiting;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for configuring rate limiting optimized for low-spec nodes.
/// </summary>
public static class RateLimitingExtensions
{
    /// <summary>
    /// Adds standard rate limiting with configurable options.
    /// Uses fixed window rate limiter by default.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Optional action to configure rate limiter options.</param>
    /// <returns>The configured builder.</returns>
    public static IHostApplicationBuilder AddStandardRateLimiting(
        this IHostApplicationBuilder builder,
        Action<RateLimiterOptions>? configure = null)
    {
        // Memory-optimized defaults for n1-standard-1 nodes (1 vCPU, 3.75GB RAM)
        var options = new RateLimiterOptions
        {
            PermitLimit = 100,
            WindowMinutes = 1,
            QueueLimit = 5, // Reduced from 10 to save memory
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            UseGlobalLimiter = false // Disabled by default to reduce overhead
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

                    // Use sliding window with reduced segments for memory efficiency
                    return RateLimitPartition.GetSlidingWindowLimiter(
                        partitionKey: partitionKey,
                        factory: _ => new SlidingWindowRateLimiterOptions
                        {
                            PermitLimit = options.PermitLimit,
                            Window = TimeSpan.FromMinutes(options.WindowMinutes),
                            SegmentsPerWindow = 4, // Reduced from 6 to save memory
                            QueueProcessingOrder = options.QueueProcessingOrder,
                            QueueLimit = options.QueueLimit
                        });
                });
            }

            // Default policy using sliding window for better traffic smoothing
            rateLimiterOptions.AddSlidingWindowLimiter("default", limiterOptions =>
            {
                limiterOptions.PermitLimit = 100;
                limiterOptions.Window = TimeSpan.FromMinutes(1);
                limiterOptions.SegmentsPerWindow = 4; // Reduced from 6 to save memory
                limiterOptions.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiterOptions.QueueLimit = 5; // Reduced from 10 for memory
            });

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

            // Custom rejection handler with detailed error information
            rateLimiterOptions.OnRejected = async (context, cancellationToken) =>
            {
                context.HttpContext.Response.StatusCode = 429;
                await context.HttpContext.Response.WriteAsJsonAsync(new
                {
                    error = "Too many requests",
                    message = "Rate limit exceeded. Please try again later.",
                    retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter)
                        ? retryAfter.ToString()
                        : "60"
                }, cancellationToken);
            };
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
