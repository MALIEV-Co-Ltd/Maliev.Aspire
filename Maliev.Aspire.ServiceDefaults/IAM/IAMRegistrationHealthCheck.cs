using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Maliev.Aspire.ServiceDefaults.IAM;

/// <summary>
/// Health check that reports IAM registration status
/// </summary>
public class IAMRegistrationHealthCheck : IHealthCheck
{
    private readonly IAMRegistrationStatusTracker _statusTracker;

    public IAMRegistrationHealthCheck(IAMRegistrationStatusTracker statusTracker)
    {
        _statusTracker = statusTracker ?? throw new ArgumentNullException(nameof(statusTracker));
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var status = _statusTracker.Status;
        var data = new Dictionary<string, object>
        {
            ["RegistrationStatus"] = status.ToString()
        };

        return status switch
        {
            RegistrationStatus.Registered =>
                Task.FromResult(HealthCheckResult.Healthy("IAM registration successful", data)),

            RegistrationStatus.Pending or RegistrationStatus.Attempting =>
                Task.FromResult(HealthCheckResult.Degraded("IAM registration in progress", null, data)),

            RegistrationStatus.PartiallyRegistered =>
                Task.FromResult(HealthCheckResult.Degraded(
                    $"IAM registration failed after multiple retries. Service running with cached permissions only.",
                    _statusTracker.LastException,
                    data)),

            RegistrationStatus.Failed =>
                Task.FromResult(HealthCheckResult.Degraded(
                    $"IAM registration failed: {_statusTracker.LastException?.Message}",
                    _statusTracker.LastException,
                    data)),

            _ => Task.FromResult(HealthCheckResult.Unhealthy("Unknown registration status", null, data))
        };
    }
}
