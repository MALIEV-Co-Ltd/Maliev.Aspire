using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace Maliev.Aspire.ServiceDefaults.Middleware;

/// <summary>
/// Middleware that catches unhandled exceptions and returns appropriate JSON error responses.
/// Maps common exception types to HTTP status codes and includes stack traces in development.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    /// <summary>
    /// Initializes a new instance of the ExceptionHandlingMiddleware.
    /// </summary>
    /// <param name="next">The next middleware in the pipeline.</param>
    /// <param name="logger">The logger for exception handling.</param>
    /// <param name="environment">The host environment to determine if running in development.</param>
    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    /// <summary>
    /// Processes the HTTP request and catches any unhandled exceptions.
    /// </summary>
    /// <param name="context">The HTTP context.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // A disconnected caller is not an application failure; do not write to an aborted response.
        }
        catch (Exception ex)
        {
            // The inner request scope has unwound; retain only its validated correlation identifier.
            var correlation = context.Items["CorrelationId"] as string;
            using var failureScope = _logger.BeginScope(new Dictionary<string, object?>
            {
                ["CorrelationId"] = Guid.TryParse(correlation, out var id) ? id.ToString("N") : null,
            });
            var (status, _) = MapExceptionToResponse(ex);
            if ((int)status >= 500 || ex is TimeoutException or OperationCanceledException or InvalidOperationException or ArgumentNullException)
            {
                _logger.LogError(new EventId(5100, "UnhandledRequestFailure"), ex,
                    "{EventName} Operation={Operation} StatusCode={StatusCode}",
                    "UnhandledRequestFailure", "HttpRequest", (int)status);
            }
            if (ex is Maliev.Diagnostics.ProductionObservabilityDiagnosticException diagnostic)
            {
                context.Response.Headers["X-Maliev-Diagnostic-Id"] = diagnostic.DiagnosticId;
                _logger.LogCritical(new EventId(5102, "ObservabilityDiagnosticFailure"), ex,
                    "{EventName}", "ObservabilityDiagnosticFailure");
            }
            await HandleExceptionAsync(context, ex);
        }
    }
    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        if (context.Response.HasStarted)
        {
            // The terminal failure was already logged above; avoid a duplicate record.
            return;
        }

        var (statusCode, message) = MapExceptionToResponse(exception);

        var response = new
        {
            Error = message,
            StatusCode = (int)statusCode,
            Details = _environment.IsDevelopment() ? exception.ToString() : null,
            TraceId = context.TraceIdentifier
        };

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = (int)statusCode;

        var json = JsonSerializer.Serialize(response, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }

    private (HttpStatusCode StatusCode, string Message) MapExceptionToResponse(Exception exception)
    {
        return exception switch
        {
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized, "Unauthorized access"),
            ArgumentNullException => (HttpStatusCode.BadRequest, exception.Message),
            ArgumentException => (HttpStatusCode.BadRequest, exception.Message),
            KeyNotFoundException => (HttpStatusCode.NotFound, "Resource not found"),
            InvalidOperationException => (HttpStatusCode.BadRequest, exception.Message),
            NotImplementedException => (HttpStatusCode.NotImplemented, "Feature not implemented"),
            TimeoutException => (HttpStatusCode.RequestTimeout, "Request timeout"),
            OperationCanceledException => (HttpStatusCode.RequestTimeout, "Request was cancelled"),

            // Database constraint violations (PostgreSQL unique_violation error code 23505)
            // Maps database-level duplicate key errors to 409 Conflict for better client experience
            _ when IsPostgresUniqueConstraintViolation(exception) => (HttpStatusCode.Conflict, ExtractConstraintMessage(exception)),

            // Support for common domain exceptions via name matching if they aren't in this project
            _ when exception.GetType().Name.Contains("NotFoundException") => (HttpStatusCode.NotFound, exception.Message),
            _ when exception.GetType().Name.Contains("ConflictException") || exception.GetType().Name.Contains("DuplicateInquiryException") => (HttpStatusCode.Conflict, exception.Message),
            _ when exception.GetType().Name.Contains("ServiceUnavailableException") || exception.GetType().Name.Contains("CountryServiceException") => (HttpStatusCode.ServiceUnavailable, exception.Message),
            _ when exception.GetType().Name.Contains("ValidationException") => (HttpStatusCode.BadRequest, exception.Message),

            _ => (HttpStatusCode.InternalServerError, "An internal server error occurred")
        };
    }

    /// <summary>
    /// Checks if the exception is a PostgreSQL unique constraint violation (error code 23505)
    /// </summary>
    private bool IsPostgresUniqueConstraintViolation(Exception exception)
    {
        // Check if it's Npgsql.PostgresException with SqlState 23505 (unique_violation)
        var exceptionType = exception.GetType();
        if (exceptionType.FullName == "Npgsql.PostgresException")
        {
            var sqlStateProperty = exceptionType.GetProperty("SqlState");
            var sqlState = sqlStateProperty?.GetValue(exception)?.ToString();
            return sqlState == "23505"; // unique_violation error code
        }

        // Check inner exceptions for wrapped database exceptions
        if (exception.InnerException != null)
        {
            return IsPostgresUniqueConstraintViolation(exception.InnerException);
        }

        return false;
    }

    /// <summary>
    /// Extracts a user-friendly message from PostgreSQL constraint violation
    /// </summary>
    private string ExtractConstraintMessage(Exception exception)
    {
        var exceptionType = exception.GetType();

        // Try to extract constraint name for more specific error messages
        if (exceptionType.FullName == "Npgsql.PostgresException")
        {
            var constraintNameProperty = exceptionType.GetProperty("ConstraintName");
            var constraintName = constraintNameProperty?.GetValue(exception)?.ToString();

            if (!string.IsNullOrEmpty(constraintName))
            {
                // Convert constraint names to user-friendly messages
                // Example: IX_Customers_Email -> "Email already exists"
                if (constraintName.Contains("Email", StringComparison.OrdinalIgnoreCase))
                {
                    return "A record with this email already exists";
                }
                if (constraintName.Contains("Username", StringComparison.OrdinalIgnoreCase))
                {
                    return "A record with this username already exists";
                }
                if (constraintName.Contains("InvoiceNumber", StringComparison.OrdinalIgnoreCase))
                {
                    return "A record with this invoice number already exists";
                }

                // Generic message if we can't determine the specific field
                return "A record with this value already exists. Please use a unique value.";
            }
        }

        // Check inner exception
        if (exception.InnerException != null && IsPostgresUniqueConstraintViolation(exception.InnerException))
        {
            return ExtractConstraintMessage(exception.InnerException);
        }

        return "A duplicate record already exists";
    }
}
