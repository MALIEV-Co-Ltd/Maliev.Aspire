using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Scalar.AspNetCore;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for configuring API documentation (OpenAPI + Scalar) in the application.
/// </summary>
public static class ApiDocumentationExtensions
{
    /// <summary>
    /// Placeholder for API documentation setup.
    /// Services must call AddEndpointsApiExplorer() and AddOpenApi("v1") themselves
    /// in their Program.cs for XML comments to work via source generator.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The configured builder.</returns>
    public static IHostApplicationBuilder AddApiDocumentation(this IHostApplicationBuilder builder)
    {
        // Empty - services handle AddOpenApi registration themselves
        return builder;
    }

    /// <summary>
    /// Maps OpenAPI and Scalar endpoints with optional service prefix.
    /// Only maps in Development and Staging environments.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <param name="servicePrefix">Optional service prefix (e.g., "auth" for /auth/openapi).</param>
    /// <param name="documentName">The OpenAPI document name (default: "v1").</param>
    /// <param name="configureScalar">Optional action to configure Scalar options.</param>
    /// <returns>The configured application.</returns>
    public static WebApplication MapApiDocumentation(
        this WebApplication app,
        string? servicePrefix = null,
        string documentName = "v1",
        Action<ScalarOptions>? configureScalar = null)
    {
        if (app.Environment.IsProduction())
        {
            return app;
        }

        var prefix = string.IsNullOrEmpty(servicePrefix) ? "" : $"/{servicePrefix}";
        var openApiPattern = $"{prefix}/openapi/{{documentName}}.json";
        var scalarEndpoint = $"{prefix}/scalar";

        // Resolved path for Scalar to fetch the OpenAPI document
        var resolvedOpenApiPath = $"{prefix}/openapi/{documentName}.json";

        // Map OpenAPI endpoint with the pattern
        app.MapOpenApi(openApiPattern);

        // Map Scalar UI with explicit OpenAPI route pattern
        app.MapScalarApiReference(scalarEndpoint, opt =>
        {
            opt.Title = string.IsNullOrEmpty(servicePrefix)
                ? "API Documentation"
                : $"{servicePrefix.ToUpper()} API Documentation";

            // Set OpenAPI document location (Scalar 2.x property)
            opt.OpenApiRoutePattern = resolvedOpenApiPath;

            // Apply custom configuration if provided
            configureScalar?.Invoke(opt);
        });

        return app;
    }

    /// <summary>
    /// Maps OpenAPI and Scalar endpoints at standard locations (/openapi, /scalar).
    /// Only maps in Development and Staging environments.
    /// </summary>
    /// <param name="app">The web application.</param>
    /// <param name="documentName">The OpenAPI document name (default: "v1").</param>
    /// <param name="configureScalar">Optional action to configure Scalar options.</param>
    /// <returns>The configured application.</returns>
    public static WebApplication MapApiDocumentationDefault(
        this WebApplication app,
        string documentName = "v1",
        Action<ScalarOptions>? configureScalar = null)
    {
        return app.MapApiDocumentation(servicePrefix: null, documentName: documentName, configureScalar: configureScalar);
    }
}
