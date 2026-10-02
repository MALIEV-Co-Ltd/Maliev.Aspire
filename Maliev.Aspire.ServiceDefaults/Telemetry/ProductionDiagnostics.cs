using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Console;

namespace Maliev.Diagnostics;

/// <summary>Writes compact diagnostic metadata without arbitrary messages, payloads or exception text.</summary>
public class ProductionConsoleFormatter : ConsoleFormatter
{
    /// <summary>The native console formatter name.</summary>
    public const string FormatterName = "maliev-error-json";

    /// <summary>Initializes the production formatter.</summary>
    public ProductionConsoleFormatter() : base(FormatterName) { }

    /// <inheritdoc/>
    public override void Write<TState>(in LogEntry<TState> entry, IExternalScopeProvider? scopes, TextWriter writer)
    {
        if (entry.LogLevel < LogLevel.Warning || entry.LogLevel == LogLevel.None) return;
        using var buffer = new MemoryStream();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteString("severity", entry.LogLevel == LogLevel.Critical ? "CRITICAL" : entry.LogLevel == LogLevel.Error ? "ERROR" : "WARNING");
            json.WriteString("occurredAtUtc", DateTimeOffset.UtcNow.ToString("O"));
            json.WriteString("logger", entry.Category);
            json.WriteNumber("eventId", entry.EventId.Id);
            json.WriteString("message", "Application diagnostic; see event, exception and trace metadata");
            var assembly = Assembly.GetEntryAssembly();
            json.WriteString("service", assembly?.GetName().Name);
            json.WriteString("deploymentVersion", assembly?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);
            if (entry.Exception is { } exception)
            {
                json.WriteString("exceptionType", exception.GetType().FullName);
                json.WriteString("innerExceptionType", exception.InnerException?.GetType().FullName);
                json.WriteString("sourceLocation", exception.TargetSite?.DeclaringType?.FullName + "." + exception.TargetSite?.Name);
            }
            else
            {
                var caller = new StackTrace().GetFrames().Select(frame => frame.GetMethod())
                    .FirstOrDefault(method => method?.DeclaringType?.Namespace?.StartsWith("Maliev.", StringComparison.Ordinal) == true
                        && method.DeclaringType != typeof(ProductionConsoleFormatter));
                json.WriteString("sourceLocation", caller?.DeclaringType?.FullName + "." + caller?.Name);
            }
            if (Activity.Current is { } activity && activity.TraceId != default)
            {
                json.WriteString("traceId", activity.TraceId.ToHexString());
                json.WriteString("spanId", activity.SpanId.ToHexString());
            }
            if (entry.Exception is ProductionObservabilityDiagnosticException diagnostic)
            {
                json.WriteBoolean("Synthetic", true);
                json.WriteString("DiagnosticId", diagnostic.DiagnosticId);
            }
            WriteSafeFields(json, entry.State);
            scopes?.ForEachScope((scope, output) => WriteSafeFields(output, scope), json);
            json.WriteEndObject();
        }
        writer.WriteLine(Encoding.UTF8.GetString(buffer.ToArray()));
    }

    private static void WriteSafeFields(Utf8JsonWriter json, object? state)
    {
        if (state is not IEnumerable<KeyValuePair<string, object?>> fields) return;
        foreach (var field in fields)
        {
            if (field.Key is "StatusCode" or "ElapsedMs" or "AttemptCount" && field.Value is int or long)
            {
                json.WriteNumber(field.Key, Convert.ToInt64(field.Value));
            }
            else if (field.Key is "EventName" or "Dependency" or "Operation" or "Method" && field.Value is string text
                && Regex.IsMatch(text, "^[A-Za-z][A-Za-z0-9_.-]{0,95}$"))
            {
                json.WriteString(field.Key, text);
            }
            else if (field.Key is "TraceId" or "SpanId" && field.Value is string trace
                && Regex.IsMatch(trace, field.Key == "TraceId" ? "^[a-f0-9]{32}$" : "^[a-f0-9]{16}$")
                && trace.Any(character => character != '0'))
            {
                json.WriteString(field.Key, trace);
            }
            else if (field.Key == "CorrelationId" && field.Value is string correlation && Guid.TryParse(correlation, out var id))
            {
                json.WriteString(field.Key, id.ToString("N"));
            }
            else if (field.Key == "Synthetic" && field.Value is true)
            {
                json.WriteBoolean(field.Key, true);
            }
            else if (field.Key == "DiagnosticId" && field.Value is string nonce && Guid.TryParseExact(nonce, "N", out var diagnosticId))
            {
                json.WriteString(field.Key, diagnosticId.ToString("N"));
            }
        }
    }
}

/// <summary>Creates the same protected log sink before host configuration can fail.</summary>
public static class ProductionLoggerFactory
{
    /// <summary>Creates a native bootstrap logger that emits only compact important diagnostic metadata.</summary>
    public static ILoggerFactory CreateBootstrapLogger() => LoggerFactory.Create(logging =>
    {
        logging.ClearProviders();
        logging.AddConsoleFormatter<ProductionConsoleFormatter, ConsoleFormatterOptions>();
        logging.AddConsole(options => options.FormatterName = ProductionConsoleFormatter.FormatterName);
        logging.AddFilter<ConsoleLoggerProvider>((_, level) => level >= LogLevel.Warning);
    });
}

/// <summary>Records one terminal dependency failure after retries, without request URLs or contents.</summary>
public sealed class DependencyFailureHandler : DelegatingHandler
{
    private readonly ILogger _logger;
    private readonly string _dependency;
    private readonly Func<CancellationToken, bool>? _expectedCancellation;

    /// <summary>Initializes a handler for a configured, code-owned client name.</summary>
    public DependencyFailureHandler(ILogger<DependencyFailureHandler> logger, string? dependency, Func<CancellationToken, bool>? expectedCancellation = null)
    {
        _logger = logger;
        _dependency = dependency != null && Regex.IsMatch(dependency, "^[A-Za-z][A-Za-z0-9_.-]{0,95}$") ? dependency : "ConfiguredHttpClient";
        _expectedCancellation = expectedCancellation;
    }

    /// <inheritdoc/>
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            if ((int)response.StatusCode >= 500) Record(null, (int)response.StatusCode);
            return response;
        }
        catch (OperationCanceledException) when (_expectedCancellation?.Invoke(cancellationToken) ?? cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception) when (exception is HttpRequestException or TimeoutException or OperationCanceledException
            || exception.GetType().Name is "TimeoutRejectedException" or "BrokenCircuitException")
        {
            Record(exception, exception is OperationCanceledException or TimeoutException ? 504 : 503);
            throw;
        }
    }

    private void Record(Exception? exception, int status) => _logger.LogError(new EventId(5101, "DependencyRequestFailure"), exception,
        "{EventName} Dependency={Dependency} Operation={Operation} StatusCode={StatusCode}",
        "DependencyRequestFailure", _dependency, "HttpRequest", status);
}
