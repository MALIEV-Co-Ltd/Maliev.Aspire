using Maliev.Aspire.ServiceDefaults.IAM;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Maliev.Aspire.ServiceDefaults.Tests.IAM;

/// <summary>
/// Unit tests for IAM registration health check classification.
/// </summary>
public class IAMRegistrationHealthCheckTests
{
    /// <summary>
    /// Verifies that startup registration states are degraded instead of unhealthy.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CheckHealthAsync_StartupRegistrationState_ReturnsDegraded(bool isAttempting)
    {
        var tracker = new IAMRegistrationStatusTracker();
        if (isAttempting)
        {
            tracker.MarkAttempting();
        }

        var healthCheck = new IAMRegistrationHealthCheck(tracker);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }

    /// <summary>
    /// Verifies that successful registration is healthy.
    /// </summary>
    [Fact]
    public async Task CheckHealthAsync_Registered_ReturnsHealthy()
    {
        var tracker = new IAMRegistrationStatusTracker();
        tracker.MarkRegistered();
        var healthCheck = new IAMRegistrationHealthCheck(tracker);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Healthy, result.Status);
    }

    /// <summary>
    /// Verifies that failed registration remains degraded for operator visibility.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CheckHealthAsync_FailedRegistrationState_ReturnsDegraded(bool isPartialFailure)
    {
        var tracker = new IAMRegistrationStatusTracker();
        var exception = new InvalidOperationException("registration failed");
        if (isPartialFailure)
        {
            tracker.MarkPartiallyRegistered(exception);
        }
        else
        {
            tracker.MarkFailed(exception);
        }

        var healthCheck = new IAMRegistrationHealthCheck(tracker);

        var result = await healthCheck.CheckHealthAsync(new HealthCheckContext());

        Assert.Equal(HealthStatus.Degraded, result.Status);
    }
}
