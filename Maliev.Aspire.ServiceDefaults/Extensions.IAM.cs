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
    public static IServiceCollection AddIAMClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
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
            // Default resilience handler is configured with 5m total timeout in AddServiceDefaults
            var timeout = iamConfig.GetValue<int?>("Timeout") ?? 300000; // 5 minutes default (allows for retries)
            client.Timeout = TimeSpan.FromMilliseconds(timeout);
        });

        httpClientBuilder.AddHttpMessageHandler(sp =>
        {
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var startupLogger = loggerFactory.CreateLogger("IAM.Handler.Factory");

            try
            {
                startupLogger.LogDebug("ServiceAccountAuthenticationHandler factory invoked for {ServiceName}", serviceName);

                // Resolve dependencies at request time (after AddIAMRegistration has registered the token provider)
                var tokenProvider = sp.GetRequiredService<IServiceAccountTokenProvider>();
                startupLogger.LogDebug("Token provider resolved successfully for {ServiceName}", serviceName);

                var logger = sp.GetRequiredService<ILogger<ServiceAccountAuthenticationHandler>>();
                var handler = new ServiceAccountAuthenticationHandler(tokenProvider, logger);

                startupLogger.LogDebug("ServiceAccountAuthenticationHandler created successfully for {ServiceName}", serviceName);
                return handler;
            }
            catch (Exception ex)
            {
                startupLogger.LogError(ex, "FAILED to create ServiceAccountAuthenticationHandler for {ServiceName}", serviceName);
                throw;
            }
        });

        httpClientBuilder.AddServiceDiscovery(); // Enable service discovery for IAM client

        // Note: Standard resilience handler is already applied by ConfigureHttpClientDefaults in AddServiceDefaults()
        // No need to add a duplicate handler here

        return services;
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