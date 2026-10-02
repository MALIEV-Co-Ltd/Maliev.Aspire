using System.Diagnostics;
using System.Net;
using System.Text.Json;
using Maliev.Diagnostics;
using Maliev.Aspire.ServiceDefaults.Middleware;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Http.Resilience;

namespace Maliev.Aspire.ServiceDefaults.Tests;

/// <summary>Exercises production privacy, terminal failures, cancellation and quiet routine traffic.</summary>
[Collection("ProductionConsole")]
public class ProductionLoggingTests
{
    /// <summary>Severity is machine readable and raw messages/scopes are never serialized.</summary>
    [Theory]
    [InlineData(LogLevel.Warning, "WARNING")]
    [InlineData(LogLevel.Error, "ERROR")]
    [InlineData(LogLevel.Critical, "CRITICAL")]
    public void Formatter_ImportantFailure_EmitsSafeSeverity(LogLevel level, string severity)
    {
        using var activity = new Activity("test").Start();
        var scopes = new LoggerExternalScopeProvider();
        using var scope = scopes.Push(new Dictionary<string, object?> { ["Email"] = "customer@example.test", ["RequestPath"] = "/upload/private-file.step", ["CorrelationId"] = Guid.NewGuid().ToString() });
        var state = new Dictionary<string, object?> { ["EventName"] = "DependencyRequestFailure", ["Dependency"] = "PdfService", ["StatusCode"] = 503, ["Payload"] = "private-payload", ["{OriginalFormat}"] = "private diagnostic template", ["Message"] = "secret" };
        var entry = new LogEntry<Dictionary<string, object?>>(level, "Maliev.Test", new EventId(5101), state, new InvalidOperationException("secret token"), (_, _) => "private message");
        using var output = new StringWriter();
        new ProductionConsoleFormatter().Write(entry, scopes, output);
        using var record = JsonDocument.Parse(output.ToString());
        Assert.Equal(severity, record.RootElement.GetProperty("severity").GetString());
        Assert.Equal("DependencyRequestFailure", record.RootElement.GetProperty("EventName").GetString());
        Assert.Equal(activity.TraceId.ToHexString(), record.RootElement.GetProperty("traceId").GetString());
        Assert.Equal(typeof(InvalidOperationException).FullName, record.RootElement.GetProperty("exceptionType").GetString());
        Assert.DoesNotContain("secret", output.ToString());
        Assert.DoesNotContain("customer@", output.ToString());
        Assert.DoesNotContain("private", output.ToString());
    }

    /// <summary>Successful or debug records cannot bloat production stdout.</summary>
    [Theory]
    [InlineData(LogLevel.Information)]
    [InlineData(LogLevel.Debug)]
    [InlineData(LogLevel.Trace)]
    public void Formatter_RoutineLog_IsQuiet(LogLevel level)
    {
        var entry = new LogEntry<string>(level, "test", default, "success", null, (state, _) => state);
        using var output = new StringWriter();
        new ProductionConsoleFormatter().Write(entry, null, output);
        Assert.Equal(string.Empty, output.ToString());
    }

    /// <summary>Returned terminal 5xx records exactly once, while expected statuses stay quiet.</summary>
    [Theory]
    [InlineData(200, 0)]
    [InlineData(400, 0)]
    [InlineData(401, 0)]
    [InlineData(404, 0)]
    [InlineData(503, 1)]
    [InlineData(504, 1)]
    public async Task Dependency_FinalResponse_ClassifiesFailure(int status, int count)
    {
        var logger = new CaptureLogger<DependencyFailureHandler>();
        var handler = new DependencyFailureHandler(logger, "CountryService") { InnerHandler = new TerminalHandler(_ => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status))) };
        using var client = new HttpClient(handler);
        var response = await client.GetAsync("https://private.test/customer/123?token=secret");
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal(count, logger.Count);
        Assert.DoesNotContain("secret", logger.LastState ?? "");
    }

    /// <summary>One failure after an inner retry sequence is emitted, not each attempt.</summary>
    [Fact]
    public async Task Dependency_RetryPipeline_EmitsOnlyTerminalFailure()
    {
        var logger = new CaptureLogger<DependencyFailureHandler>();
        int attempts = 0;
        var services = new ServiceCollection();
        services.AddHttpClient("CountryService")
            .AddHttpMessageHandler(() => new DependencyFailureHandler(logger, "CountryService"))
            .ConfigurePrimaryHttpMessageHandler(() => new TerminalHandler(_ => { attempts++; return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)); }))
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = 2;
                options.Retry.Delay = TimeSpan.Zero;
                options.Retry.UseJitter = false;
            });
        using var provider = services.BuildServiceProvider();
        using var client = provider.GetRequiredService<IHttpClientFactory>().CreateClient("CountryService");
        await client.GetAsync("https://private.test/");
        Assert.Equal(3, attempts);
        Assert.Equal(1, logger.Count);
    }

    /// <summary>Client aborts stay quiet; timeout failures preserve the original exception and ERROR.</summary>
    [Theory]
    [InlineData(true, 0)]
    [InlineData(false, 1)]
    public async Task Dependency_Cancellation_DistinguishesExpectedAbort(bool expected, int count)
    {
        var logger = new CaptureLogger<DependencyFailureHandler>();
        var exception = new OperationCanceledException("private details");
        var handler = new DependencyFailureHandler(logger, "CountryService", _ => expected) { InnerHandler = new TerminalHandler(_ => Task.FromException<HttpResponseMessage>(exception)) };
        using var client = new HttpClient(handler);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync("https://private.test/"));
        Assert.Equal(count, logger.Count);
    }

    /// <summary>Global request handling preserves status, logging only application failures.</summary>
    [Theory]
    [InlineData(true, 400, 0)]
    [InlineData(false, 500, 1)]
    public async Task ExceptionBoundary_ExpectedClientError_IsQuiet(bool expected, int status, int count)
    {
        var logger = new CaptureLogger<ExceptionHandlingMiddleware>();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var environment = new FakeEnvironment();
        Exception failure = expected ? new ArgumentException("invalid") : new Exception("private details");
        var middleware = new ExceptionHandlingMiddleware(_ => Task.FromException(failure), logger, environment);
        await middleware.InvokeAsync(context);
        Assert.Equal(status, context.Response.StatusCode);
        Assert.Equal(count, logger.Count);
        if (count > 0) Assert.Equal(LogLevel.Error, logger.LastLevel);
        Assert.DoesNotContain("private details", logger.LastState ?? "");
    }

    /// <summary>Production providers retain a single safe console sink even with an OTLP endpoint configured.</summary>
    [Fact]
    public void ServiceDefaults_Production_UsesOneConsoleLogSink()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Production });
        builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] = "http://localhost:4317";
        builder.AddServiceDefaults();
        using var host = builder.Build();
        var providers = host.Services.GetServices<ILoggerProvider>().ToArray();
        Assert.Single(providers);
        Assert.Equal("ConsoleLoggerProvider", providers[0].GetType().Name);
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Maliev.Business");
        Assert.False(logger.IsEnabled(LogLevel.Information));
        Assert.True(logger.IsEnabled(LogLevel.Error));
        Assert.True(host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Microsoft.EntityFrameworkCore.Database.Command").IsEnabled(LogLevel.Error));
    }

    /// <summary>The defaults registration resolves the actual client name and records after real resilience retries.</summary>
    [Theory]
    [InlineData("CountryService")]
    [InlineData("")]
    public async Task ServiceDefaults_NamedAndDefaultClients_RecordOneTerminalFailure(string name)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Production });
        builder.AddServiceDefaults();
        var logger = new CaptureLogger<DependencyFailureHandler>();
        builder.Services.AddSingleton<ILogger<DependencyFailureHandler>>(logger);
        int attempts = 0;
        builder.Services.AddHttpClient(name).ConfigurePrimaryHttpMessageHandler(() => new TerminalHandler(_ =>
        {
            attempts++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        }));
        builder.Services.ConfigureAll<HttpStandardResilienceOptions>(options =>
        {
            options.Retry.MaxRetryAttempts = 2;
            options.Retry.Delay = TimeSpan.Zero;
            options.Retry.UseJitter = false;
        });
        using var host = builder.Build();
        using var client = host.Services.GetRequiredService<IHttpClientFactory>().CreateClient(name);
        var response = await client.GetAsync("https://example.invalid/private?secret=hidden");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(3, attempts);
        Assert.Equal(1, logger.Count);
        Assert.Contains(string.IsNullOrEmpty(name) ? "ConfiguredHttpClient" : name, logger.LastState);
        Assert.DoesNotContain("private", logger.LastState);
        Assert.DoesNotContain("hidden", logger.LastState);
    }

    /// <summary>Proxies cannot spoof loopback diagnostic access.</summary>
    [Theory]
    [InlineData("203.0.113.1", "GET")]
    [InlineData("127.0.0.1", "POST")]
    public async Task Diagnostic_ExternalOrWriteRequest_IsQuiet404(string address, string method)
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Parse(address);
        context.Request.Method = method;
        context.Request.Path = ProductionObservabilityMiddleware.DiagnosticPath;
        context.Request.Headers["X-Maliev-Diagnostic-Id"] = Guid.NewGuid().ToString("N");
        context.Request.Headers["X-Forwarded-For"] = "127.0.0.1";
        var logger = new CaptureLogger<ProductionObservabilityMiddleware>();
        var middleware = new ProductionObservabilityMiddleware(_ => throw new Exception("Must not enter application"), logger);
        await middleware.InvokeAsync(context);
        Assert.Equal(404, context.Response.StatusCode);
        Assert.Equal(0, logger.Count);
    }

    /// <summary>The controlled exception uses the real JSON failure boundary and is rate limited.</summary>
    [Fact]
    public async Task Diagnostic_Loopback_ExercisesExceptionBoundaryAndBudget()
    {
        var diagnosticLogger = new CaptureLogger<ProductionObservabilityMiddleware>();
        var exceptionLogger = new CaptureLogger<ExceptionHandlingMiddleware>();
        var probe = new ProductionObservabilityMiddleware(_ => Task.CompletedTask, diagnosticLogger);
        var boundary = new ExceptionHandlingMiddleware(probe.InvokeAsync, exceptionLogger, new FakeEnvironment());
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = IPAddress.Loopback;
        context.Request.Method = "GET";
        context.Request.Path = ProductionObservabilityMiddleware.DiagnosticPath;
        var id = Guid.NewGuid().ToString("N");
        context.Request.Headers["X-Maliev-Diagnostic-Id"] = id;
        context.Response.Body = new MemoryStream();
        await boundary.InvokeAsync(context);
        Assert.Equal(500, context.Response.StatusCode);
        Assert.Equal(id, context.Response.Headers["X-Maliev-Diagnostic-Id"]);
        Assert.Equal(1, diagnosticLogger.Count);
        Assert.Equal(2, exceptionLogger.Count);
        Assert.Equal(LogLevel.Critical, exceptionLogger.LastLevel);
        context.Response.StatusCode = 200;
        await boundary.InvokeAsync(context);
        Assert.Equal(429, context.Response.StatusCode);
        Assert.Equal(1, diagnosticLogger.Count);
        Assert.Equal(2, exceptionLogger.Count);
    }

    /// <summary>Explicit 5xx results are captured without logging routine client rejections.</summary>
    [Theory]
    [InlineData(200, 0)]
    [InlineData(400, 0)]
    [InlineData(503, 1)]
    public async Task ResponseBoundary_HandledStatus_CapturesOnlyServerFailure(int status, int count)
    {
        var logger = new CaptureLogger<ProductionObservabilityMiddleware>();
        var middleware = new ProductionObservabilityMiddleware(context => { context.Response.StatusCode = status; return Task.CompletedTask; }, logger);
        await middleware.InvokeAsync(new DefaultHttpContext());
        Assert.Equal(count, logger.Count);
    }

    /// <summary>Probe failures emit a bounded signal, and recovery allows the next outage to be detected.</summary>
    [Fact]
    public async Task ResponseBoundary_ProbeFailure_DeduplicatesUntilRecovery()
    {
        var logger = new CaptureLogger<ProductionObservabilityMiddleware>();
        int status = 503;
        var middleware = new ProductionObservabilityMiddleware(context => { context.Response.StatusCode = status; return Task.CompletedTask; }, logger);
        var context = new DefaultHttpContext();
        context.Request.Path = "/country/readiness";
        await middleware.InvokeAsync(context);
        await middleware.InvokeAsync(context);
        Assert.Equal(1, logger.Count);
        status = 200;
        await middleware.InvokeAsync(context);
        Assert.Equal(1, logger.Count);
        status = 503;
        await middleware.InvokeAsync(context);
        Assert.Equal(2, logger.Count);
    }

    /// <summary>A terminal exception retains the same validated correlation identifier returned to the caller.</summary>
    [Fact]
    public async Task ExceptionBoundary_UnwoundScope_RetainsClientCorrelation()
    {
        var logger = new CaptureLogger<ExceptionHandlingMiddleware>();
        var context = new DefaultHttpContext();
        var id = Guid.NewGuid().ToString("N");
        context.Request.Headers["X-Correlation-ID"] = id;
        context.Response.Body = new MemoryStream();
        var correlation = new CorrelationIdMiddleware(_ => Task.FromException(new Exception("private")), NullLogger<CorrelationIdMiddleware>.Instance);
        var boundary = new ExceptionHandlingMiddleware(correlation.InvokeAsync, logger, new FakeEnvironment());
        await boundary.InvokeAsync(context);
        Assert.Equal(id, context.Response.Headers["X-Correlation-ID"]);
        using var record = JsonDocument.Parse(logger.LastJson!);
        Assert.Equal(id, record.RootElement.GetProperty("CorrelationId").GetString());
    }

    /// <summary>HttpClient's linked timeout token is not mistaken for an incoming caller abort in a worker.</summary>
    [Fact]
    public async Task ServiceDefaults_WorkerHttpClientTimeout_RecordsFailure()
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Production });
        builder.AddServiceDefaults();
        var logger = new CaptureLogger<DependencyFailureHandler>();
        builder.Services.AddSingleton<ILogger<DependencyFailureHandler>>(logger);
        builder.Services.AddHttpClient("WorkerDependency", client => client.Timeout = TimeSpan.FromMilliseconds(50))
            .ConfigurePrimaryHttpMessageHandler(() => new DelayedHandler());
        using var host = builder.Build();
        using var client = host.Services.GetRequiredService<IHttpClientFactory>().CreateClient("WorkerDependency");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GetAsync("https://example.invalid/"));
        Assert.Equal(1, logger.Count);
        Assert.Equal(LogLevel.Error, logger.LastLevel);
    }

    /// <summary>Legacy 400 mapping must not hide unexpected internal state failures.</summary>
    [Fact]
    public async Task ExceptionBoundary_InvalidInternalState_PreservesErrorEvidence()
    {
        var logger = new CaptureLogger<ExceptionHandlingMiddleware>();
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        var boundary = new ExceptionHandlingMiddleware(_ => Task.FromException(new InvalidOperationException("No authenticationScheme configured; private details")), logger, new FakeEnvironment());
        await boundary.InvokeAsync(context);
        Assert.Equal(400, context.Response.StatusCode);
        Assert.Equal(1, logger.Count);
        Assert.Equal(LogLevel.Error, logger.LastLevel);
        Assert.DoesNotContain("private", logger.LastJson);
    }

    /// <summary>Invalid state trace lengths and all-zero values cannot become correlation metadata.</summary>
    [Theory]
    [InlineData("TraceId", "0123456789abcdef")]
    [InlineData("TraceId", "0123456789abcdef0123")]
    [InlineData("TraceId", "00000000000000000000000000000000")]
    [InlineData("SpanId", "0123456789abcdef0123456789abcdef")]
    public void Formatter_InvalidTraceState_IsExcluded(string key, string value)
    {
        var entry = new LogEntry<Dictionary<string, object?>>(LogLevel.Error, "test", default, new() { [key] = value }, null, (_, _) => "error");
        using var output = new StringWriter();
        new ProductionConsoleFormatter().Write(entry, null, output);
        using var record = JsonDocument.Parse(output.ToString());
        Assert.False(record.RootElement.TryGetProperty(key, out _));
    }

    /// <summary>Startup logging uses the protected severity/privacy policy before a host exists.</summary>
    [Fact]
    public void BootstrapFactory_StartupFailure_IsCriticalWithoutPayload()
    {
        var original = Console.Out;
        using var output = new StringWriter();
        try
        {
            Console.SetOut(output);
            using (var factory = ProductionLoggerFactory.CreateBootstrapLogger())
            {
                var logger = factory.CreateLogger("Startup");
                Assert.False(logger.IsEnabled(LogLevel.Information));
                logger.LogCritical(new Exception("private credential"), "{EventName} {Payload}", "StartupFailure", "customer-secret");
            }
            using var record = JsonDocument.Parse(output.ToString());
            Assert.Equal("CRITICAL", record.RootElement.GetProperty("severity").GetString());
            Assert.Equal("StartupFailure", record.RootElement.GetProperty("EventName").GetString());
            Assert.DoesNotContain("private", output.ToString());
            Assert.DoesNotContain("customer", output.ToString());
        }
        finally { Console.SetOut(original); }
    }

    private sealed class DelayedHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private sealed class TerminalHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }

    private sealed class CaptureLogger<T> : ILogger<T>
    {
        public int Count { get; private set; }
        public string? LastState { get; private set; }
        public LogLevel LastLevel { get; private set; }
        private readonly LoggerExternalScopeProvider _scopes = new();
        public string? LastJson { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => _scopes.Push(state);
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Count++;
            LastLevel = logLevel;
            LastState = formatter(state, null);
            using var output = new StringWriter();
            var entry = new LogEntry<TState>(logLevel, typeof(T).FullName!, eventId, state, exception, formatter);
            new ProductionConsoleFormatter().Write(entry, _scopes, output);
            LastJson = output.ToString();
        }
    }

    private sealed class FakeEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Production;
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = ".";
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } = new Microsoft.Extensions.FileProviders.NullFileProvider();
    }
}

/// <summary>Serializes native console redirection tests against other test collections.</summary>
[CollectionDefinition("ProductionConsole", DisableParallelization = true)]
public sealed class ProductionConsoleCollection { }
