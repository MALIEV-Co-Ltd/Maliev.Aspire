using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Scalar.AspNetCore;
using System.Globalization;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for configuring API documentation (OpenAPI + Scalar) in the application.
/// </summary>
public static class ApiDocumentationExtensions
{
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

        IEndpointRouteBuilder endpoints = app;
        var urlPrefix = "";

        if (!string.IsNullOrEmpty(servicePrefix))
        {
            endpoints = app.MapGroup(servicePrefix);
            urlPrefix = $"/{servicePrefix}";
        }

        // Map OpenAPI endpoint
        endpoints.MapOpenApi("/openapi/{documentName}.json");

        // Map Scalar UI
        endpoints.MapScalarApiReference("/scalar", opt =>
        {
            TextInfo documentationTitle = new CultureInfo("en-US").TextInfo;
            opt.Title = string.IsNullOrEmpty(servicePrefix)
                ? "API Documentation"
                : $"{documentationTitle.ToTitleCase(servicePrefix)} API Documentation";

            // Set OpenAPI document location (Scalar 2.x property)
            // This must be the absolute path that the browser can use to fetch the spec
            opt.OpenApiRoutePattern = $"{urlPrefix}/openapi/{documentName}.json";

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
