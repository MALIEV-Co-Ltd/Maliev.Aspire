using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Maliev.Aspire.ServiceDefaults.Middleware;

namespace Microsoft.Extensions.Hosting;

public static class MiddlewareExtensions
{
    /// <summary>
    /// Adds standard middleware services to the application.
    /// Call this in Program.cs before building the app.
    /// </summary>
    public static IHostApplicationBuilder AddStandardMiddleware(
        this IHostApplicationBuilder builder,
        Action<MiddlewareOptions>? configure = null)
    {
        var options = new MiddlewareOptions();
        configure?.Invoke(options);

        builder.Services.AddSingleton(options);

        return builder;
    }

    /// <summary>
    /// Uses standard middleware in the application pipeline.
    /// Call this in Program.cs after app.Build() and before other middleware.
    /// Order: CorrelationId → SecurityHeaders → ExceptionHandling → RequestLogging
    /// </summary>
    public static IApplicationBuilder UseStandardMiddleware(this IApplicationBuilder app)
    {
        var options = app.ApplicationServices.GetService<MiddlewareOptions>() ?? new MiddlewareOptions();

        // Order matters!
        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseMiddleware<SecurityHeadersMiddleware>();

        if (options.EnableRequestLogging)
        {
            app.UseMiddleware<RequestLoggingMiddleware>();
        }

        app.UseMiddleware<ExceptionHandlingMiddleware>();

        return app;
    }
}

public class MiddlewareOptions
{
    /// <summary>
    /// Enable request/response logging (can be verbose).
    /// Default: false
    /// </summary>
    public bool EnableRequestLogging { get; set; } = false;

    /// <summary>
    /// Include stack traces in error responses in development.
    /// Default: true
    /// </summary>
    public bool IncludeStackTraceInDevelopment { get; set; } = true;

    /// <summary>
    /// Correlation ID header name.
    /// Default: X-Correlation-ID
    /// </summary>
    public string CorrelationIdHeaderName { get; set; } = "X-Correlation-ID";

    /// <summary>
    /// Custom security headers to add/override.
    /// </summary>
    public Dictionary<string, string> CustomSecurityHeaders { get; set; } = new();
}
