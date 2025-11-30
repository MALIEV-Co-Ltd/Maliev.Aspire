using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for configuring CORS policies in the application.
/// </summary>
public static class CorsExtensions
{
    /// <summary>
    /// Adds CORS with default policy reading from configuration.
    /// Reads allowed origins from "CORS:AllowedOrigins" configuration (comma-separated).
    /// Falls back to localhost:3000 if not configured.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="policyName">Optional custom policy name (defaults to default policy).</param>
    /// <returns>The configured builder.</returns>
    public static IHostApplicationBuilder AddDefaultCors(
        this IHostApplicationBuilder builder,
        string? policyName = null)
    {
        var corsOrigins = builder.Configuration["CORS:AllowedOrigins"]?.Split(',', StringSplitOptions.RemoveEmptyEntries)
            ?? new[] { "http://localhost:3000" };

        builder.Services.AddCors(options =>
        {
            options.AddPolicy(policyName ?? "__DefaultCorsPolicy", policy =>
            {
                policy.WithOrigins(corsOrigins)
                      .AllowAnyMethod()
                      .AllowAnyHeader()
                      .AllowCredentials();
            });

            // Also set as default policy if no custom name provided
            if (policyName == null)
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.WithOrigins(corsOrigins)
                          .AllowAnyMethod()
                          .AllowAnyHeader()
                          .AllowCredentials();
                });
            }
        });

        return builder;
    }

    /// <summary>
    /// Adds CORS with custom configuration.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">Action to configure CORS options.</param>
    /// <returns>The configured builder.</returns>
    public static IHostApplicationBuilder AddCorsWithOptions(
        this IHostApplicationBuilder builder,
        Action<Microsoft.AspNetCore.Cors.Infrastructure.CorsOptions> configure)
    {
        builder.Services.AddCors(configure);
        return builder;
    }
}
