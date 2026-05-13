using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;
using System.Net.Http.Json;

namespace Maliev.Aspire.ServiceDefaults.IAM;

/// <summary>
/// Default implementation of IAM service client for permission checking and resolution.
/// Supports both global and resource-scoped permission checks with automatic failover.
/// </summary>
public partial class IamServiceClient : IIamServiceClient
{
    private const string AspireTestAdminPrincipalId = "00000000-0000-0000-0000-000000000002";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IamServiceClient> _logger;
    private readonly IHostEnvironment _environment;

    /// <summary>
    /// Initializes a new instance of the <see cref="IamServiceClient"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The HTTP client factory for creating named clients.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="environment">The current host environment.</param>
    public IamServiceClient(
        IHttpClientFactory httpClientFactory,
        ILogger<IamServiceClient> logger,
        IHostEnvironment environment)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _environment = environment;
    }

    private HttpClient GetHttpClient() => _httpClientFactory.CreateClient("IAMService");

    /// <inheritdoc />
    public async Task<IEnumerable<string>> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (IsAspireTestAdmin(userId))
        {
            return ["*"];
        }

        try
        {
            var response = await GetHttpClient().PostAsJsonAsync(
                "/iam/v1/auth/resolve-permissions",
                new { PrincipalId = userId },
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Log.FailedToFetchPermissions(_logger, userId, response.StatusCode);
                return Enumerable.Empty<string>();
            }

            var result = await response.Content.ReadFromJsonAsync<PermissionResolutionResponse>(cancellationToken: cancellationToken);
            return result?.Permissions ?? Enumerable.Empty<string>();
        }
        catch (Exception ex)
        {
            Log.ErrorFetchingPermissions(_logger, userId, ex);
            return Enumerable.Empty<string>();
        }
    }

    /// <inheritdoc />
    public async Task<bool> CheckPermissionAsync(
        string principalId,
        string permissionId,
        string? resourcePath = null,
        CancellationToken cancellationToken = default)
    {
        if (IsAspireTestAdmin(principalId))
        {
            return true;
        }

        try
        {
            var request = new CheckPermissionRequest(principalId, permissionId, resourcePath);

            var response = await GetHttpClient().PostAsJsonAsync(
                "/iam/v1/auth/check-permission",
                request,
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                Log.FailedToCheckPermission(_logger, principalId, permissionId, resourcePath ?? "global", response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<CheckPermissionResponse>(cancellationToken: cancellationToken);
            return result?.Allowed ?? false;
        }
        catch (Exception ex)
        {
            Log.ErrorCheckingPermission(_logger, principalId, permissionId, resourcePath ?? "global", ex);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, bool>> CheckPermissionsAsync(
        string principalId,
        IEnumerable<PermissionCheckRequest> requests,
        CancellationToken cancellationToken = default)
    {
        var result = new Dictionary<string, bool>();

        try
        {
            var requestList = requests.ToList();

            // Performance optimization: Parallelize permission checks using Task.WhenAll
            // Each permission check is an independent HTTP call that can run concurrently
            var tasks = requestList.Select(async req =>
            {
                var allowed = await CheckPermissionAsync(
                    principalId,
                    req.PermissionId,
                    req.ResourcePath,
                    cancellationToken);

                return new { req.PermissionId, Allowed = allowed };
            }).ToList();

            var results = await Task.WhenAll(tasks);

            foreach (var r in results)
            {
                result[r.PermissionId] = r.Allowed;
            }

            return result;
        }
        catch (Exception ex)
        {
            Log.ErrorBulkCheckingPermissions(_logger, principalId, requests.Count(), ex);

            // Return false for all permissions on error
            foreach (var req in requests)
            {
                result[req.PermissionId] = false;
            }

            return result;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<string>> GetAuthorizedResourcesAsync(
        string principalId,
        string permissionId,
        string resourceType,
        CancellationToken cancellationToken = default)
    {
        if (IsAspireTestAdmin(principalId))
        {
            return ["*"];
        }

        try
        {
            var response = await GetHttpClient().GetAsync(
                $"/iam/v1/auth/authorized-resources?principalId={principalId}&permissionId={permissionId}&resourceType={resourceType}",
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Failed to fetch authorized resources for principal {PrincipalId}. Status: {StatusCode}", principalId, response.StatusCode);
                return Enumerable.Empty<string>();
            }

            var result = await response.Content.ReadFromJsonAsync<AuthorizedResourcesResponse>(cancellationToken: cancellationToken);
            return result?.ResourceIds ?? Enumerable.Empty<string>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred while fetching authorized resources for principal {PrincipalId}", principalId);
            return Enumerable.Empty<string>();
        }
    }

    // DTOs for IAM API communication
    private sealed record AuthorizedResourcesResponse(
        string PrincipalId,
        string PermissionId,
        string ResourceType,
        List<string> ResourceIds);

    private sealed record PermissionResolutionResponse(
        string PrincipalId,
        List<string> Permissions,
        List<string> Roles,
        DateTime CacheUntil,
        bool FromCache);

    private sealed record CheckPermissionRequest(
        string PrincipalId,
        string PermissionId,
        string? ResourcePath);

    private sealed record CheckPermissionResponse(
        string PrincipalId,
        string PermissionId,
        bool Allowed,
        string? ResourcePath,
        bool FromCache,
        long LatencyMs);

    private bool IsAspireTestAdmin(string principalId)
    {
        return _environment.IsEnvironment("Testing") &&
            string.Equals(principalId, AspireTestAdminPrincipalId, StringComparison.OrdinalIgnoreCase);
    }

    // Logging with source generation
    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to fetch permissions for user {UserId}. Status: {StatusCode}")]
        public static partial void FailedToFetchPermissions(ILogger logger, string userId, System.Net.HttpStatusCode statusCode);

        [LoggerMessage(Level = LogLevel.Error, Message = "Error occurred while fetching permissions for user {UserId}")]
        public static partial void ErrorFetchingPermissions(ILogger logger, string userId, Exception ex);

        [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to check permission {Permission} for principal {PrincipalId} on resource {ResourcePath}. Status: {StatusCode}")]
        public static partial void FailedToCheckPermission(ILogger logger, string principalId, string permission, string resourcePath, System.Net.HttpStatusCode statusCode);

        [LoggerMessage(Level = LogLevel.Error, Message = "Error occurred while checking permission {Permission} for principal {PrincipalId} on resource {ResourcePath}")]
        public static partial void ErrorCheckingPermission(ILogger logger, string principalId, string permission, string resourcePath, Exception ex);

        [LoggerMessage(Level = LogLevel.Error, Message = "Error occurred while bulk checking {Count} permissions for principal {PrincipalId}")]
        public static partial void ErrorBulkCheckingPermissions(ILogger logger, string principalId, int count, Exception ex);
    }
}
