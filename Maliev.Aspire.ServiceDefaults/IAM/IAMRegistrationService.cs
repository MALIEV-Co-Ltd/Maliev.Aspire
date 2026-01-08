using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Maliev.Aspire.ServiceDefaults.IAM;

/// <summary>
/// Base class for services to register permissions and roles with IAM.
/// Registration is called by BackgroundIAMRegistrationService after service startup.
/// </summary>
public abstract class IAMRegistrationService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceAccountTokenProvider _tokenProvider;
    private readonly ILogger _logger;
    private readonly string _serviceName;

    /// <summary>
    /// Initializes a new instance of the <see cref="IAMRegistrationService"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory</param>
    /// <param name="tokenProvider">The token provider</param>
    /// <param name="logger">The logger</param>
    /// <param name="serviceName">The service name</param>
    protected IAMRegistrationService(
        IHttpClientFactory httpClientFactory,
        IServiceAccountTokenProvider tokenProvider,
        ILogger logger,
        string serviceName)
    {
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        _tokenProvider = tokenProvider ?? throw new ArgumentNullException(nameof(tokenProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceName = serviceName ?? throw new ArgumentNullException(nameof(serviceName));
    }

    protected abstract IEnumerable<PermissionRegistration> GetPermissions();
    protected abstract IEnumerable<RoleRegistration> GetPredefinedRoles();

    /// <summary>
    /// Registers permissions and roles with the IAM service.
    /// Called by BackgroundIAMRegistrationService with retry logic.
    /// </summary>
    public async Task RegisterAsync(CancellationToken cancellationToken)
    {
        // Staggered startup: Random delay 0-5 seconds to avoid overwhelming IAM database
        var delayMs = Random.Shared.Next(0, 5000);
        _logger.LogInformation("Starting IAM registration for {ServiceName} (staggered delay: {DelayMs}ms)...", _serviceName, delayMs);
        await Task.Delay(delayMs, cancellationToken);

        var permissions = GetPermissions().ToList();
        ValidatePermissions(permissions);

        var roles = GetPredefinedRoles().ToList();

        if (permissions.Any())
        {
            await RegisterPermissionsAsync(permissions, cancellationToken);
        }

        if (roles.Any())
        {
            await RegisterRolesAsync(roles, cancellationToken);
        }

        _logger.LogInformation("Successfully registered permissions and roles for {ServiceName} with IAM.", _serviceName);
    }

    private async Task RegisterPermissionsAsync(IEnumerable<PermissionRegistration> permissions, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("IAMService");
        var token = _tokenProvider.GetToken();

        var payload = new
        {
            ServiceName = _serviceName,
            Permissions = permissions
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/iam/v1/permissions/register")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("ServiceAccount", token);

        var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private async Task RegisterRolesAsync(IEnumerable<RoleRegistration> roles, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("IAMService");
        var token = _tokenProvider.GetToken();

        var payload = new
        {
            ServiceName = _serviceName,
            Roles = roles
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "/iam/v1/roles/register")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("ServiceAccount", token);

        var response = await client.SendAsync(request, cancellationToken);
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
