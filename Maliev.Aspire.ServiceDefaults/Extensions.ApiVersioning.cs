using Asp.Versioning;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for configuring API versioning in the application.
/// </summary>
public static class ApiVersioningExtensions
{
    /// <summary>
    /// Adds default API versioning configuration with URL segment versioning.
    /// Configures v1.0 as default, assumes default version when unspecified,
    /// reports API versions in response headers, and uses URL segment reader.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The configured builder.</returns>
    public static IHostApplicationBuilder AddDefaultApiVersioning(this IHostApplicationBuilder builder)
    {
        builder.Services.AddApiVersioning(options =>
        {
            options.DefaultApiVersion = new ApiVersion(1, 0);
            options.AssumeDefaultVersionWhenUnspecified = true;
            options.ReportApiVersions = true;
            options.ApiVersionReader = new UrlSegmentApiVersionReader();
        }).AddApiExplorer(options =>
        {
            options.GroupNameFormat = "'v'VVV";
            options.SubstituteApiVersionInUrl = true;
        });

        return builder;
    }
}
