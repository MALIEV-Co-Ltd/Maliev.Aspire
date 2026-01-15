using Maliev.Aspire.ServiceDefaults;
using Maliev.Aspire.ServiceDefaults.IAM;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

// Standardized HTTP client extensions for Maliev microservices.
public static class HttpClientExtensions
{
    /// <summary>
    /// Adds IAM service HTTP client with standard resilience and service account authentication.
    /// </summary>
    /// <param name="builder">The application builder.</param>
    /// <param name="serviceName">Optional explicit service name. If null, reads from "ServiceName" config.</param>
    public static IHostApplicationBuilder AddIAMServiceClient(
        this IHostApplicationBuilder builder,
        string? serviceName = null)
    {
        // Require explicit name via parameter or configuration
        // REMOVED multiple fallbacks to ensure standardized configuration
        var finalServiceName = serviceName
            ?? builder.Configuration["ServiceName"]
            ?? throw new InvalidOperationException("Service name must be provided to AddIAMServiceClient via the 'serviceName' parameter or configuration key 'ServiceName'.");

        // Register the named client "IAMService" with full configuration (resilience + service account auth)
        // This uses the AddIAMClient extension which configures auth handler and resilience
        builder.Services.AddIAMClient(builder.Configuration, finalServiceName);

        // Register the typed client using the same named configuration.
        // It will automatically inherit the auth handler and resilience configured in AddIAMClient.
        builder.Services.AddHttpClient<IIamServiceClient, IamServiceClient>("IAMService");

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
    /// Adds a typed service HTTP client with ENFORCED configuration pattern.
    /// REQUIRED: Services:{ServiceName}:BaseUrl must be configured (no fallbacks).
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
            // ENFORCED PATTERN: Services:{ServiceName}:BaseUrl (no fallbacks)
            var url = configuration[$"Services:{serviceName}:BaseUrl"]
                ?? throw new InvalidOperationException(
                    $"Required configuration 'Services:{serviceName}:BaseUrl' is missing. Check appsettings.json or environment variables.");

            client.BaseAddress = new Uri(url);
            client.Timeout = TimeSpan.FromSeconds(90);
        });

        // Note: Standard resilience handler is already applied by ConfigureHttpClientDefaults in AddServiceDefaults()
        // No need to add a duplicate handler here

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
    /// Adds generic service HTTP client with ENFORCED configuration pattern.
    /// REQUIRED: Services:{ServiceName}:BaseUrl must be configured (no fallbacks).
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
            // ENFORCED PATTERN: Services:{ServiceName}:BaseUrl (no fallbacks)
            // Only allow explicit baseUrl parameter for test overrides
            var url = baseUrl
                ?? configuration[$"Services:{serviceName}:BaseUrl"]
                ?? throw new InvalidOperationException(
                    $"Required configuration 'Services:{serviceName}:BaseUrl' is missing. Check appsettings.json or environment variables.");

            client.BaseAddress = new Uri(url);
            client.Timeout = TimeSpan.FromSeconds(90);

            configureClient?.Invoke(client);
        });

        // Note: Standard resilience handler is already applied by ConfigureHttpClientDefaults in AddServiceDefaults()
        // No need to add a duplicate handler here

        return httpClientBuilder;
    }
}