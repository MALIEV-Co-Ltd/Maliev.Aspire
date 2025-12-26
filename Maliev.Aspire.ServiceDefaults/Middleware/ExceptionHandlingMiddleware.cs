using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;

namespace Maliev.Aspire.ServiceDefaults.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Request was cancelled");
            await HandleExceptionAsync(context, new OperationCanceledException("Request was cancelled"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unhandled exception occurred: {Message}", ex.Message);
            Console.WriteLine($"DEBUG EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine(ex.StackTrace);
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
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
            
            // Support for common domain exceptions via name matching if they aren't in this project
            _ when exception.GetType().Name.Contains("NotFoundException") => (HttpStatusCode.NotFound, exception.Message),
            _ when exception.GetType().Name.Contains("ConflictException") || exception.GetType().Name.Contains("DuplicateInquiryException") => (HttpStatusCode.Conflict, exception.Message),
            _ when exception.GetType().Name.Contains("ServiceUnavailableException") || exception.GetType().Name.Contains("CountryServiceException") => (HttpStatusCode.ServiceUnavailable, exception.Message),
            _ when exception.GetType().Name.Contains("ValidationException") => (HttpStatusCode.BadRequest, exception.Message),
            
            _ => (HttpStatusCode.InternalServerError, "An internal server error occurred")
        };
    }
}
