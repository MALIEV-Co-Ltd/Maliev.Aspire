using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Scalar.AspNetCore;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for configuring API documentation (OpenAPI + Scalar) in the application.
/// </summary>
public static class ApiDocumentationExtensions
{
    /// <summary>
    /// Adds OpenAPI (Swagger) documentation with Scalar UI.
    /// Only enabled in Development and Staging environments.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The configured builder.</returns>
    public static IHostApplicationBuilder AddApiDocumentation(this IHostApplicationBuilder builder)
    {
        if (builder.Environment.IsDevelopment() || builder.Environment.IsStaging())
        {
            builder.Services.AddEndpointsApiExplorer();
            builder.Services.AddOpenApi();
        }

        return builder;
    }

    /// <summary>
    /// Maps OpenAPI and Scalar endpoints with optional service prefix.
    /// Only maps in Development and Staging environments.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <param name="servicePrefix">Optional service prefix (e.g., "auth" for /auth/openapi).</param>
    /// <param name="configureScalar">Optional action to configure Scalar options.</param>
    /// <returns>The configured application.</returns>
    public static WebApplication MapApiDocumentation(
        this WebApplication app,
        string? servicePrefix = null,
        Action<ScalarOptions>? configureScalar = null)
    {
        if (!app.Environment.IsDevelopment() && !app.Environment.IsStaging())
        {
            return app;
        }

        var prefix = string.IsNullOrEmpty(servicePrefix) ? "" : $"/{servicePrefix}";
        var openApiPath = $"{prefix}/openapi/{{documentName}}.json";
        var scalarPath = $"{prefix}/scalar";

        // Map OpenAPI endpoint
        app.MapOpenApi(openApiPath);

        // Map Scalar UI
        app.MapScalarApiReference(options =>
        {
            options.WithOpenApiRoutePattern(openApiPath);

            // Set custom endpoint path
            options.EndpointPathPrefix = prefix;

            // Apply custom configuration if provided
            configureScalar?.Invoke(options);
        });

        return app;
    }

    /// <summary>
    /// Maps OpenAPI and Scalar endpoints at standard locations (/openapi, /scalar).
    /// Only maps in Development and Staging environments.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <param name="configureScalar">Optional action to configure Scalar options.</param>
    /// <returns>The configured application.</returns>
    public static WebApplication MapApiDocumentationDefault(
        this WebApplication app,
        Action<ScalarOptions>? configureScalar = null)
    {
        return app.MapApiDocumentation(servicePrefix: null, configureScalar: configureScalar);
    }
}
