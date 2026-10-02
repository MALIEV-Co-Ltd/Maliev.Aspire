#nullable enable
using System;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Maliev.Diagnostics;

/// <summary>Exercises the real failure boundary without calling a business operation.</summary>
public sealed class ProductionObservabilityMiddleware
{
    /// <summary>The direct loopback diagnostic route.</summary>
    public const string DiagnosticPath = "/internal/diagnostics/observability";
    private readonly RequestDelegate _next;
    private readonly ILogger<ProductionObservabilityMiddleware> _logger;
    private readonly object _gate = new object();
    private DateTimeOffset _lastProbeAtUtc = DateTimeOffset.MinValue;
    private readonly System.Collections.Generic.Dictionary<string, DateTimeOffset> _failedProbeLastLog = new();

    /// <summary>Initializes the diagnostic boundary.</summary>
    public ProductionObservabilityMiddleware(RequestDelegate next, ILogger<ProductionObservabilityMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    /// <summary>Requires direct loopback, GET and a strict nonce before emitting synthetic evidence.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path != DiagnosticPath)
        {
            await _next(context);
            // Exceptions unwind to the outer failure boundary; only completed 5xx responses reach here.
            var path = context.Request.Path.Value ?? "/";
            var segment = path.Substring(path.LastIndexOf('/') + 1);
            var probe = segment is "readiness" or "liveness" or "health" or "aspire-liveness";
            var emit = context.Response.StatusCode >= 500;
            if (probe)
            {
                lock (_gate)
                {
                    if (!emit) _failedProbeLastLog.Remove(segment);
                    else if (_failedProbeLastLog.TryGetValue(segment, out var last) && DateTimeOffset.UtcNow - last < TimeSpan.FromMinutes(5)) emit = false;
                    else _failedProbeLastLog[segment] = DateTimeOffset.UtcNow;
                }
            }
            if (emit)
            {
                _logger.LogError(new EventId(5103, "HandledOperationFailure"),
                    "{EventName} Operation={Operation} StatusCode={StatusCode}",
                    "HandledOperationFailure", probe ? "HealthProbe" : "HttpResponse", context.Response.StatusCode);
            }
            return;
        }
        context.Response.Headers.CacheControl = "no-store";
        context.Response.Headers["X-Robots-Tag"] = "noindex";
        if (!HttpMethods.IsGet(context.Request.Method)
            || context.Connection.RemoteIpAddress == null
            || !IPAddress.IsLoopback(context.Connection.RemoteIpAddress)
            || !Guid.TryParseExact(context.Request.Headers["X-Maliev-Diagnostic-Id"], "N", out Guid id))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }
        lock (_gate)
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            if (now - _lastProbeAtUtc < TimeSpan.FromMinutes(1))
            {
                context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                return;
            }
            _lastProbeAtUtc = now;
        }
        string diagnosticId = id.ToString("N");
        context.Response.Headers["X-Maliev-Diagnostic-Id"] = diagnosticId;
        _logger.LogWarning("{EventName} Synthetic={Synthetic} DiagnosticId={DiagnosticId}",
            "ObservabilityPipelineProbe", true, diagnosticId);
        throw new ProductionObservabilityDiagnosticException(diagnosticId);
    }
}

/// <summary>Identifies a controlled diagnostic failure rather than a customer incident.</summary>
public sealed class ProductionObservabilityDiagnosticException : Exception
{
    /// <summary>Initializes a controlled failure with its validated nonce.</summary>
    public ProductionObservabilityDiagnosticException(string diagnosticId)
        : base("Controlled loopback observability diagnostic; no business operation executed.")
    {
        DiagnosticId = diagnosticId;
    }

    /// <summary>Gets the validated diagnostic nonce.</summary>
    public string DiagnosticId { get; }
}
