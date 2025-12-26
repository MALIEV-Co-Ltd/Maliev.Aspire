using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.AspNetCore.Authorization;
using Maliev.Aspire.ServiceDefaults.Authorization;
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
            var iamConfig = configuration.GetSection("IAM");
            var baseUrl = iamConfig["BaseUrl"] ?? "http://maliev-iamservice-api";

            client.BaseAddress = new Uri(baseUrl);
            client.DefaultRequestHeaders.Add("X-Service-Name", serviceName);

            var timeout = iamConfig.GetValue<int?>("Timeout") ?? 30000;
            client.Timeout = TimeSpan.FromMilliseconds(timeout);

            var token = iamConfig["ServiceAccountToken"];
            if (!string.IsNullOrEmpty(token))
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            }
        })
        .AddStandardResilienceHandler();

        return services;
    }

    /// <summary>
    /// Adds permission-based authorization infrastructure.
    /// </summary>
    public static IServiceCollection AddPermissionAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder();
        services.AddHttpContextAccessor();
#pragma warning disable ASPDEPR006
        services.AddSingleton<Microsoft.AspNetCore.Mvc.Infrastructure.IActionContextAccessor, Microsoft.AspNetCore.Mvc.Infrastructure.ActionContextAccessor>();
#pragma warning restore ASPDEPR006
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();

        return services;
    }
}