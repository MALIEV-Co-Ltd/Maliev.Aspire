using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Maliev.Aspire.ServiceDefaults.Middleware;

public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeaderName = "X-Correlation-ID";
    private readonly RequestDelegate _next;
    private readonly ILogger<CorrelationIdMiddleware> _logger;

    public CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var correlationId = GetOrCreateCorrelationId(context);

        // Add to response headers
        context.Response.Headers.TryAdd(CorrelationIdHeaderName, correlationId);

        // Add to HttpContext.Items for access in controllers/services
        context.Items["CorrelationId"] = correlationId;

        // Create logging scope
        var scopeProperties = new Dictionary<string, object?>
        {
            ["CorrelationId"] = correlationId,
            ["RequestPath"] = context.Request.Path.Value,
            ["RequestMethod"] = context.Request.Method,
            ["UserAgent"] = context.Request.Headers.UserAgent.ToString(),
            ["RemoteIp"] = context.Connection.RemoteIpAddress?.ToString()
        };

        using (_logger.BeginScope(scopeProperties))
        {
            await _next(context);
        }
    }

    private string GetOrCreateCorrelationId(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(CorrelationIdHeaderName, out var correlationId))
        {
            return correlationId.ToString();
        }

        return Guid.NewGuid().ToString();
    }
}
