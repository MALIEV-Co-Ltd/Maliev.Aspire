using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.AspNetCore.Authorization;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.Aspire.ServiceDefaults.IAM;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;
using Polly;

namespace Maliev.Aspire.ServiceDefaults;

/// <summary>
/// Extensions for IAM client configuration.
/// </summary>
public static class IAMExtensions
{
    /// <summary>
    /// Adds and configures a resilient IAM client with service account authentication.
    /// </summary>
    public static IHttpClientBuilder AddIAMClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        // Register the token provider so the handler can resolve it
        services.TryAddSingleton<IServiceAccountTokenProvider>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            return new ServiceAccountTokenProvider(config, serviceName);
        });

        // Register the handler in DI so it can be resolved by the HttpClient builder
        services.TryAddTransient<ServiceAccountAuthenticationHandler>();

        var httpClientBuilder = services.AddHttpClient("IAMService", client =>
        {
            var iamConfig = configuration.GetSection("IAM");

            // ENFORCED PATTERN: Services:IAMService:BaseUrl (no fallbacks)
            var baseUrl = configuration["Services:IAMService:BaseUrl"]
                ?? throw new InvalidOperationException(
                    "Required configuration 'Services:IAMService:BaseUrl' is missing. Check appsettings.json or environment variables.");

            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("X-Service-Name", serviceName);

            // Timeout must be >= TotalRequestTimeout (5m) to let resilience handler control retries
            var timeout = iamConfig.GetValue<int?>("Timeout") ?? 300000; // 5 minutes default
            client.Timeout = TimeSpan.FromMilliseconds(timeout);
        });

        // Add the authentication handler from DI
        httpClientBuilder.AddHttpMessageHandler<ServiceAccountAuthenticationHandler>();

        httpClientBuilder.AddServiceDiscovery(); // Enable service discovery for IAM client

        return httpClientBuilder;
    }

    /// <summary>
    /// Adds permission-based authorization infrastructure.
    /// </summary>
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();

        // Register authorization services first
        services.AddAuthorization();

        // CRITICAL: Replace the default policy provider with our custom one
        // Using Replace ensures our provider is used even if a default one was registered
        services.Replace(ServiceDescriptor.Singleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>());
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }
}