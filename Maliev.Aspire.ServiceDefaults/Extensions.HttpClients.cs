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
        builder.Services.AddHttpClient<IIamServiceClient, IamServiceClient>((sp, client) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var iamUrl = config["ExternalServices:IAM:BaseUrl"]
                ?? config.GetConnectionString("iam-service")
                ?? "http://iam-service:8080";

            client.BaseAddress = new Uri(iamUrl);
            client.Timeout = TimeSpan.FromSeconds(30);

            // Add service account token if configured
            var token = config["ExternalServices:IAM:ServiceAccountToken"];
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        })
        .AddStandardResilienceHandler();

        return builder;
    }

    /// <summary>
    /// Adds Upload service HTTP client with standard resilience.
    /// </summary>
    public static IHostApplicationBuilder AddUploadServiceClient(
        this IHostApplicationBuilder builder)
    {
        builder.Services.AddHttpClient("upload-service", (sp, client) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var uploadUrl = config["ExternalServices:Upload:BaseUrl"]
                ?? config.GetConnectionString("upload-service")
                ?? "http://upload-service:8080";

            client.BaseAddress = new Uri(uploadUrl);
            client.Timeout = TimeSpan.FromMinutes(5); // Longer timeout for file uploads
        })
        .ConfigurePrimaryHttpMessageHandler(() =>
        {
            return new HttpClientHandler
            {
                MaxRequestContentBufferSize = 100 * 1024 * 1024, // 100MB for large files
                AllowAutoRedirect = true
            };
        })
        .AddStandardResilienceHandler(options =>
        {
            options.Retry.MaxRetryAttempts = 3;
            options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
        });

        return builder;
    }

    /// <summary>
    /// Adds PDF service HTTP client with standard resilience.
    /// </summary>
    public static IHostApplicationBuilder AddPdfServiceClient(
        this IHostApplicationBuilder builder)
    {
        builder.Services.AddHttpClient("pdf-service", (sp, client) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var pdfUrl = config["ExternalServices:Pdf:BaseUrl"]
                ?? config.GetConnectionString("pdf-service")
                ?? "http://pdf-service:8080";

            client.BaseAddress = new Uri(pdfUrl);
            client.Timeout = TimeSpan.FromMinutes(2); // PDF generation can take time
        })
        .AddStandardResilienceHandler();

        return builder;
    }

    /// <summary>
    /// Adds Notification service HTTP client with standard resilience.
    /// </summary>
    public static IHostApplicationBuilder AddNotificationServiceClient(
        this IHostApplicationBuilder builder)
    {
        builder.Services.AddHttpClient("notification-service", (sp, client) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var notificationUrl = config["ExternalServices:Notification:BaseUrl"]
                ?? config.GetConnectionString("notification-service")
                ?? "http://notification-service:8080";

            client.BaseAddress = new Uri(notificationUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddStandardResilienceHandler();

        return builder;
    }

    /// <summary>
    /// Adds Customer service HTTP client with standard resilience.
    /// </summary>
    public static IHostApplicationBuilder AddCustomerServiceClient(
        this IHostApplicationBuilder builder)
    {
        builder.Services.AddHttpClient("customer-service", (sp, client) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var customerUrl = config["ExternalServices:Customer:BaseUrl"]
                ?? config.GetConnectionString("customer-service")
                ?? "http://customer-service:8080";

            client.BaseAddress = new Uri(customerUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        })
        .AddStandardResilienceHandler();

        return builder;
    }

    /// <summary>
    /// Adds generic service HTTP client with configurable name and URL.
    /// Use this for services not covered by specific helpers.
    /// </summary>
    public static IHttpClientBuilder AddServiceClient(
        this IHostApplicationBuilder builder,
        string serviceName,
        string? baseUrl = null,
        Action<HttpClient>? configureClient = null)
    {
        var httpClientBuilder = builder.Services.AddHttpClient(serviceName, (sp, client) =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var url = baseUrl
                ?? config[$"ExternalServices:{serviceName}:BaseUrl"]
                ?? config.GetConnectionString(serviceName)
                ?? $"http://{serviceName.ToLowerInvariant()}:8080";

            client.BaseAddress = new Uri(url);
            client.Timeout = TimeSpan.FromSeconds(30);

            configureClient?.Invoke(client);
        });

        httpClientBuilder.AddStandardResilienceHandler();

        return httpClientBuilder;
    }
}