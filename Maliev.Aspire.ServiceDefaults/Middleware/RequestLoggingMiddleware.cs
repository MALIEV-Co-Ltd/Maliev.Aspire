using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Maliev.Aspire.ServiceDefaults.Middleware;

public class RequestLoggingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<RequestLoggingMiddleware> _logger;

    public RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();

        if (IsHealthCheck(context.Request.Path))
        {
            await _next(context);
            return;
        }

        _logger.LogInformation(
            "HTTP {Method} {Path} started",
            context.Request.Method,
            context.Request.Path);

        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();

            _logger.LogInformation(
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs}ms",
                context.Request.Method,
                context.Request.Path,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds);
        }
    }

    private static bool IsHealthCheck(PathString path)
    {
        return path.HasValue && (
            path.Value.EndsWith("/readiness", StringComparison.OrdinalIgnoreCase) ||
            path.Value.EndsWith("/liveness", StringComparison.OrdinalIgnoreCase) ||
            path.Value.EndsWith("/aspire-liveness", StringComparison.OrdinalIgnoreCase) ||
            path.Value.EndsWith("/health", StringComparison.OrdinalIgnoreCase) ||
            path.Value.EndsWith("/metrics", StringComparison.OrdinalIgnoreCase));
    }
}
