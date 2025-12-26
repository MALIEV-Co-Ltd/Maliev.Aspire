using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Maliev.Aspire.ServiceDefaults.IAM;

/// <summary>
/// Background service that handles IAM registration with automatic retry.
/// Runs AFTER service startup completes, preventing blocking.
/// </summary>
public class BackgroundIAMRegistrationService : BackgroundService
{
    private readonly IAMRegistrationService _registrationService;
    private readonly ILogger<BackgroundIAMRegistrationService> _logger;
    private readonly IAMRegistrationStatusTracker _statusTracker;

    private static readonly TimeSpan[] RetryDelays =
    {
        TimeSpan.FromSeconds(1),
        TimeSpan.FromSeconds(2),
        TimeSpan.FromSeconds(5),
        TimeSpan.FromSeconds(10),
        TimeSpan.FromSeconds(30),
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(2)
    };

    public BackgroundIAMRegistrationService(
        IAMRegistrationService registrationService,
        ILogger<BackgroundIAMRegistrationService> logger,
        IAMRegistrationStatusTracker statusTracker)
    {
        _registrationService = registrationService ?? throw new ArgumentNullException(nameof(registrationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _statusTracker = statusTracker ?? throw new ArgumentNullException(nameof(statusTracker));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Wait a bit for service to fully start
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

        const int maxAttempts = 10;
        int attempt = 0;

        while (!stoppingToken.IsCancellationRequested && !_statusTracker.IsRegistered)
        {
            try
            {
                _logger.LogInformation("Attempting IAM registration (attempt {Attempt}/{Max})", attempt + 1, maxAttempts);
                _statusTracker.MarkAttempting();

                await _registrationService.RegisterAsync(stoppingToken);

                _statusTracker.MarkRegistered();
                _logger.LogInformation("IAM registration successful");
                return; // Success, exit loop
            }
            catch (Exception ex)
            {
                attempt++;

                if (attempt >= maxAttempts)
                {
                    _logger.LogError(ex, "IAM registration FAILED after {Max} attempts. Service will continue with potential authorization limitations.", maxAttempts);
                    _statusTracker.MarkPartiallyRegistered(ex);
                    return; // Give up after max attempts
                }

                _statusTracker.MarkAttempting(); // Still attempting
                _logger.LogWarning(ex, "IAM registration attempt {Attempt} failed", attempt);

                // Calculate retry delay with exponential backoff
                var delay = (attempt - 1) < RetryDelays.Length
                    ? RetryDelays[attempt - 1]
                    : RetryDelays[^1];

                _logger.LogInformation("Retrying IAM registration in {Delay}", delay);
                await Task.Delay(delay, stoppingToken);
            }
        }
    }
}

/// <summary>
/// Tracks IAM registration status for health check reporting
/// </summary>
public class IAMRegistrationStatusTracker
{
    private volatile RegistrationStatus _status = RegistrationStatus.Pending;
    private Exception? _lastException;

    public bool IsRegistered => _status == RegistrationStatus.Registered;
    public RegistrationStatus Status => _status;
    public Exception? LastException => _lastException;

    public void MarkAttempting() => _status = RegistrationStatus.Attempting;
    public void MarkRegistered() => _status = RegistrationStatus.Registered;
    public void MarkPartiallyRegistered(Exception ex)
    {
        _status = RegistrationStatus.PartiallyRegistered;
        _lastException = ex;
    }
    public void MarkFailed(Exception ex)
    {
        _status = RegistrationStatus.Failed;
        _lastException = ex;
    }
}

/// <summary>
/// IAM registration status enum
/// </summary>
public enum RegistrationStatus
{
    Pending,             // Initial state, not started yet
    Attempting,          // Currently trying to register
    Registered,          // Successfully registered
    PartiallyRegistered, // Failed after max retries, service continued
    Failed               // Unrecoverable failure
}
