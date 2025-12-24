using System.Net.Http.Json;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Maliev.Aspire.ServiceDefaults.IAM;

/// <summary>
/// Base class for services to register permissions and roles with IAM on startup.
/// </summary>
public abstract class IAMRegistrationService : IHostedService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger _logger;
    private readonly string _serviceName;

    /// <summary>
    /// Initializes a new instance of the <see cref="IAMRegistrationService"/> class.
    /// </summary>
    protected IAMRegistrationService(
        IHttpClientFactory httpClientFactory,
        ILogger logger,
        string serviceName)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceName = serviceName ?? throw new ArgumentNullException(nameof(serviceName));
    }

    protected abstract IEnumerable<PermissionRegistration> GetPermissions();
    protected abstract IEnumerable<RoleRegistration> GetPredefinedRoles();

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting IAM registration for {ServiceName}...", _serviceName);

            var permissions = GetPermissions().ToList();
            ValidatePermissions(permissions);

            var roles = GetPredefinedRoles().ToList();

            var client = _httpClientFactory.CreateClient("IAMService");

            if (permissions.Any())
            {
                await RegisterPermissionsAsync(client, permissions, cancellationToken);
            }

            if (roles.Any())
            {
                await RegisterRolesAsync(client, roles, cancellationToken);
            }

            _logger.LogInformation("Successfully registered permissions and roles for {ServiceName} with IAM.", _serviceName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to register with IAM service for {ServiceName}. Continuing startup.", _serviceName);
            // Fail-open: do not rethrow
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task RegisterPermissionsAsync(HttpClient client, IEnumerable<PermissionRegistration> permissions, CancellationToken cancellationToken)
    {
        var payload = new
        {
            ServiceName = _serviceName,
            Permissions = permissions
        };

        var response = await client.PostAsJsonAsync("/iam/v1/permissions/register", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task RegisterRolesAsync(HttpClient client, IEnumerable<RoleRegistration> roles, CancellationToken cancellationToken)
    {
        var payload = new
        {
            ServiceName = _serviceName,
            Roles = roles
        };

        var response = await client.PostAsJsonAsync("/iam/v1/roles/register", payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private void ValidatePermissions(IEnumerable<PermissionRegistration> permissions)
    {
        foreach (var p in permissions)
        {
            if (!IsValidPermissionFormat(p.PermissionId))
            {
                throw new InvalidOperationException($"Invalid permission format: {p.PermissionId}. Expected service.resource.action");
            }
        }
    }

    private bool IsValidPermissionFormat(string permission)
    {
        var parts = permission.Split('.');
        return parts.Length == 3 && parts.All(seg => !string.IsNullOrWhiteSpace(seg));
    }
}
