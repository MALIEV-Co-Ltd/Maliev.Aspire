using Maliev.Aspire.ServiceDefaults.IAM;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.Extensions.Hosting;

/// <summary>
/// Extension methods for IAM registration with background retry and health check integration
/// </summary>
public static class IAMRegistrationExtensions
{
    /// <summary>
    /// Adds IAM registration with background retry and health check integration.
    /// Registration happens in the background after service startup completes,
    /// preventing blocking and enabling graceful degradation if IAM service is unavailable.
    /// </summary>
    /// <typeparam name="TService">The service-specific IAM registration implementation</typeparam>
    /// <param name="services">The service collection</param>
    /// <returns>The service collection for chaining</returns>
    public static IServiceCollection AddIAMRegistration<TService>(
        this IServiceCollection services)
        where TService : IAMRegistrationService
    {
        // Register the service-specific registration implementation
        services.AddSingleton<TService>();
        services.AddSingleton<IAMRegistrationService>(sp => sp.GetRequiredService<TService>());

        // Register status tracker
        services.AddSingleton<IAMRegistrationStatusTracker>();

        // Register background service
        services.AddHostedService<BackgroundIAMRegistrationService>();

        // Add health check
        services.AddHealthChecks()
            .AddCheck<IAMRegistrationHealthCheck>("iam_registration", tags: new[] { "ready" });

        return services;
    }
}
