using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Maliev.Aspire.ServiceDefaults.IAM;
using Polly;
using Microsoft.AspNetCore.Http;
using System.Net.Http.Json;

namespace Microsoft.Extensions.Hosting;

public static class HttpClientExtensions
{
    /// <summary>
    /// Adds IAM service HTTP client with standard resilience.
    /// </summary>
    public static IHostApplicationBuilder AddIAMServiceClient(
        this IHostApplicationBuilder builder)
    {
        // Register both typed and named client
        builder.AddServiceClient<IIamServiceClient, IamServiceClient>("IAM");
        builder.AddServiceClient("IAMService");

        return builder;
    }

    /// <summary>
    /// Adds Upload service HTTP client with standard resilience.
    /// </summary>
    public static IHostApplicationBuilder AddUploadServiceClient(
        this IHostApplicationBuilder builder)
    {
        builder.AddServiceClient("UploadService", configureClient: client => 
        {
            client.Timeout = TimeSpan.FromMinutes(10);
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            return new HttpClientHandler
            {
                MaxRequestContentBufferSize = 100 * 1024 * 1024, // 100MB for large files
                AllowAutoRedirect = true
            };
        });

        return builder;
    }

    /// <summary>
    /// Adds PDF service HTTP client with standard resilience.
    /// </summary>
    public static IHostApplicationBuilder AddPdfServiceClient(
        this IHostApplicationBuilder builder)
    {
        builder.AddServiceClient("PdfService", configureClient: client => 
        {
            client.Timeout = TimeSpan.FromMinutes(2);
        });

        return builder;
    }

    /// <summary>
    /// Adds Notification service HTTP client with standard resilience.
    /// </summary>
    public static IHostApplicationBuilder AddNotificationServiceClient(
        this IHostApplicationBuilder builder)
    {
        builder.AddServiceClient("NotificationService");
        return builder;
    }

    /// <summary>
    /// Adds Customer service HTTP client with standard resilience.
    /// </summary>
    public static IHostApplicationBuilder AddCustomerServiceClient(
        this IHostApplicationBuilder builder)
    {
        builder.AddServiceClient("CustomerService");
        return builder;
    }

    /// <summary>
    /// Adds a typed service HTTP client with standardized Triple Fallback discovery and resilience.
    /// </summary>
    public static IHttpClientBuilder AddServiceClient<TInterface, TImplementation>(
        this IHostApplicationBuilder builder,
        string serviceName)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        return builder.Services.AddServiceClient<TInterface, TImplementation>(builder.Configuration, serviceName);
    }

    /// <summary>
    /// Adds a typed service HTTP client with standardized Triple Fallback discovery and resilience.
    /// </summary>
    public static IHttpClientBuilder AddServiceClient<TInterface, TImplementation>(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        var httpClientBuilder = services.AddHttpClient<TInterface, TImplementation>((sp, client) =>
        {
            var normalizedName = serviceName.ToLowerInvariant().Replace("service", "");
            var dnsName = $"maliev-{normalizedName}service-api";

            // Triple Fallback URI logic
            var url = configuration[$"{serviceName}:BaseUrl"]
                ?? configuration.GetConnectionString(serviceName)
                ?? $"http://{dnsName}";

            client.BaseAddress = new Uri(url);
            client.Timeout = TimeSpan.FromSeconds(60);
        });

        httpClientBuilder.AddStandardResilienceHandler();

        return httpClientBuilder;
    }

    /// <summary>
    /// Adds generic service HTTP client with configurable name and URL.
    /// </summary>
    public static IHttpClientBuilder AddServiceClient(
        this IHostApplicationBuilder builder,
        string serviceName,
        string? baseUrl = null,
        Action<HttpClient>? configureClient = null)
    {
        return builder.Services.AddServiceClient(builder.Configuration, serviceName, baseUrl, configureClient);
    }

    /// <summary>
    /// Adds generic service HTTP client with configurable name and URL.
    /// </summary>
    public static IHttpClientBuilder AddServiceClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName,
        string? baseUrl = null,
        Action<HttpClient>? configureClient = null)
    {
        var httpClientBuilder = services.AddHttpClient(serviceName, (sp, client) =>
        {
            var normalizedName = serviceName.ToLowerInvariant().Replace("service", "");
            var dnsName = $"maliev-{normalizedName}service-api";

            var url = baseUrl
                ?? configuration[$"{serviceName}:BaseUrl"]
                ?? configuration.GetConnectionString(serviceName)
                ?? $"http://{dnsName}";

            client.BaseAddress = new Uri(url);
            client.Timeout = TimeSpan.FromSeconds(60);

            configureClient?.Invoke(client);
        });

        httpClientBuilder.AddStandardResilienceHandler();

        return httpClientBuilder;
    }
}