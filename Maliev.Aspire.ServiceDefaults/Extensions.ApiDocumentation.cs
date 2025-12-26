using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Scalar.AspNetCore;
using System.Globalization;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for configuring API documentation (OpenAPI + Scalar) in the application.
/// </summary>
public static class ApiDocumentationExtensions
{
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

        var urlPrefix = string.IsNullOrEmpty(servicePrefix) ? "" : $"/{servicePrefix}";
        var openApiRoute = $"{urlPrefix}/openapi/{documentName}.json";
        var scalarRoute = $"{urlPrefix}/scalar";

        app.MapOpenApi(openApiRoute);

        app.MapScalarApiReference(scalarRoute, opt =>
        {
            TextInfo documentationTitle = new CultureInfo("en-US").TextInfo;
            var displayTitle = string.IsNullOrEmpty(servicePrefix) ? "API" : servicePrefix.Replace("-", " ");
            opt.Title = $"{documentationTitle.ToTitleCase(displayTitle)} Documentation";
            opt.OpenApiRoutePattern = openApiRoute;
            configureScalar?.Invoke(opt);
        });

        return app;
    }

    public static IHostApplicationBuilder AddStandardOpenApi(
        this IHostApplicationBuilder builder, 
        string? title = null, 
        string? description = null,
        string documentName = "v1")
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddOpenApi(documentName, options =>
        {
            if (title != null || description != null)
            {
                options.AddDocumentTransformer((document, context, cancellationToken) =>
                {
                    if (title != null) document.Info.Title = title;
                    if (description != null) document.Info.Description = description;
                    return Task.CompletedTask;
                });
            }
        });

        return builder;
    }

    public static WebApplication MapApiDocumentationDefault(
        this WebApplication app,
        string documentName = "v1",
        Action<ScalarOptions>? configureScalar = null)
    {
        return app.MapApiDocumentation(servicePrefix: null, documentName: documentName, configureScalar: configureScalar);
    }
}