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
        // This uses the AddIAMClient extension which configures auth handler and service discovery
        builder.Services.AddIAMClient(builder.Configuration, finalServiceName);

        // Register the IamServiceClient which uses IHttpClientFactory to create "IAMService" clients
        builder.Services.AddScoped<IIamServiceClient, IamServiceClient>();

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
    /// Adds a typed service HTTP client with standardized discovery, resilience and Service Account authentication.
    /// </summary>
    public static IHttpClientBuilder AddAuthenticatedServiceClient<TInterface, TImplementation>(
        this IHostApplicationBuilder builder,
        string serviceName,
        string? sourceServiceName = null)
        where TInterface : class
        where TImplementation : class, TInterface
    {
        // 1. Add standard service account auth handler to the service collection
        var finalSourceServiceName = sourceServiceName
            ?? builder.Configuration["ServiceName"]
            ?? throw new InvalidOperationException("ServiceName must be configured in appsettings.json or passed as 'sourceServiceName' parameter to use AddAuthenticatedServiceClient.");

        // This configures the named client "IAMService" (used as a template)
        builder.Services.AddIAMClient(builder.Configuration, finalSourceServiceName);

        // 2. Register the typed client using the named configuration
        return builder.Services.AddHttpClient<TInterface, TImplementation>(serviceName, (sp, client) =>
        {
            // Check if there's an explicit URL configured (for GKE deployment)
            var explicitUrl = builder.Configuration[$"Services:{serviceName}:BaseUrl"];

            if (!string.IsNullOrEmpty(explicitUrl))
            {
                // Use explicit URL for GKE/production
                client.BaseAddress = new Uri(explicitUrl);
            }
            else
            {
                // Use service name for Aspire service discovery
                // Service discovery will resolve "http://{serviceName}" to actual endpoint
                client.BaseAddress = new Uri($"http://{serviceName}");
            }

            client.Timeout = TimeSpan.FromSeconds(90);
        })
        .AddServiceDiscovery() // Resolves serviceName -> BaseAddress via Aspire
        .AddHttpMessageHandler<ServiceAccountAuthenticationHandler>();
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
            // Check if there's an explicit URL configured (for GKE deployment)
            var explicitUrl = configuration[$"Services:{serviceName}:BaseUrl"];

            if (!string.IsNullOrEmpty(explicitUrl))
            {
                // Use explicit URL for GKE/production
                client.BaseAddress = new Uri(explicitUrl);
            }
            else
            {
                // Use service name for Aspire service discovery
                // Service discovery will resolve "http://{serviceName}" to actual endpoint
                client.BaseAddress = new Uri($"http://{serviceName}");
            }

            client.Timeout = TimeSpan.FromSeconds(90);
        })
        .AddServiceDiscovery(); // Resolves serviceName -> BaseAddress via Aspire

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
            // Check if there's an explicit URL configured (for GKE deployment)
            var explicitUrl = baseUrl ?? configuration[$"Services:{serviceName}:BaseUrl"];

            if (!string.IsNullOrEmpty(explicitUrl))
            {
                // Use explicit URL for GKE/production
                client.BaseAddress = new Uri(explicitUrl);
            }
            else
            {
                // Use service name for Aspire service discovery
                // Service discovery will resolve "http://{serviceName}" to actual endpoint
                client.BaseAddress = new Uri($"http://{serviceName}");
            }

            client.Timeout = TimeSpan.FromSeconds(90);

            configureClient?.Invoke(client);
        })
        .AddServiceDiscovery(); // Resolves serviceName -> BaseAddress via Aspire

        return httpClientBuilder;
    }
}
