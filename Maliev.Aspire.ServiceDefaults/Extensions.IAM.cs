using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Polly;

namespace Maliev.Aspire.ServiceDefaults;

/// <summary>
/// Extensions for IAM client configuration.
/// </summary>
public static class IAMExtensions
{
    /// <summary>
    /// Adds and configures a resilient IAM client.
    /// </summary>
    public static IServiceCollection AddIAMClient(
        this IServiceCollection services,
        IConfiguration configuration,
        string serviceName)
    {
        services.AddHttpClient("IAMService", client =>
        {
            var iamConfig = configuration.GetSection("ExternalServices:IAM");
            var baseUrl = iamConfig["BaseUrl"] ?? throw new InvalidOperationException("IAM BaseUrl not configured");

            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("X-Service-Name", serviceName);

            var timeout = iamConfig.GetValue<int?>("Timeout") ?? 5000;
            client.Timeout = TimeSpan.FromMilliseconds(timeout);

            var token = iamConfig["ServiceAccountToken"];
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {token}");
            }
        })
        .AddStandardResilienceHandler(options =>
        {
            // Configure standard resilience (Retries, Circuit Breaker, etc.)
            // By default, this adds:
            // 1. Rate Limiter
            // 2. Total Request Timeout
            // 3. Retry (3 attempts, exponential backoff)
            // 4. Circuit Breaker
            // 5. Attempt Timeout
        });

        return services;
    }
}
