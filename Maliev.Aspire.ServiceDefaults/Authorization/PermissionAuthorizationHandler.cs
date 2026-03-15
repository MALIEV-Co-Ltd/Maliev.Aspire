using Maliev.Aspire.ServiceDefaults.IAM;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace Maliev.Aspire.ServiceDefaults.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IIamServiceClient? _iamClient;
    private readonly ILogger<PermissionAuthorizationHandler> _logger;
    private readonly IAuthMetrics? _authMetrics;

    public PermissionAuthorizationHandler(
        IServiceProvider serviceProvider,
        IHttpContextAccessor httpContextAccessor,
        ILogger<PermissionAuthorizationHandler> logger,
        IIamServiceClient? iamClient = null,
        IAuthMetrics? authMetrics = null)
    {
        _serviceProvider = serviceProvider;
        _httpContextAccessor = httpContextAccessor;
        _iamClient = iamClient;
        _logger = logger;
        _authMetrics = authMetrics;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        try
        {
            if (await CheckRequirementAsync(
                context,
                requirement.Permission,
                requirement.ResourcePathTemplate,
                requirement.AuditPurpose))
            {
                context.Succeed(requirement);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception in PermissionAuthorizationHandler");
        }
    }

    private async Task<bool> CheckRequirementAsync(
        AuthorizationHandlerContext context,
        string permission,
        string? resourcePathTemplate,
        string? auditPurpose)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null) return false;

        var user = context.User;
        if (user?.Identity?.IsAuthenticated != true) return false;

        var principalId = user.FindFirst("user_id")?.Value
            ?? user.FindFirst("sub")?.Value
            ?? user.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(principalId)) return false;

        _logger.LogInformation("Checking permission {Permission} for Principal {PrincipalId} on Resource {ResourcePath}",
            permission, principalId, resourcePathTemplate ?? "global");

        bool hasPermission = false;
        string? resourcePath = null;

        // Try IAM live check first if client is available
        if (_iamClient != null)
        {
            if (!string.IsNullOrEmpty(resourcePathTemplate))
            {
                var config = _serviceProvider.GetService<IConfiguration>();
                var resourceScopedEnabled = config?.GetValue<bool>("Features:ResourceScopedAuthEnabled", false) ?? false;
                if (resourceScopedEnabled)
                {
                    resourcePath = ResolveResourcePath(resourcePathTemplate, httpContext.GetRouteData().Values);
                }
            }

            try
            {
                hasPermission = await _iamClient.CheckPermissionAsync(principalId, permission, resourcePath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "IAM service call failed for permission {Permission}", permission);
                var config = _serviceProvider.GetService<IConfiguration>();
                if (config?.GetValue<bool>("Features:FailOpenOnIAMError", false) ?? false)
                {
                    hasPermission = true;
                }
            }
        }

        // Fallback to JWT claims if IAM check failed or client not available
        if (!hasPermission)
        {
            var userPermissions = user.Claims
                .Where(c => c.Type == "permissions" || c.Type == "permission" || c.Type == "role" || c.Type == "roles" || c.Type == "http://schemas.microsoft.com/ws/2008/06/identity/claims/role")
                .Select(c => c.Value)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            // If user has wildcard permission in JWT, always allow
            if (userPermissions.Contains("*"))
            {
                _logger.LogInformation("JWT contains wildcard permission - granting access for {Permission}", permission);
                return true;
            }

            _logger.LogInformation("IAM check failed, falling back to claims. Found {Count} permission claims for Principal {PrincipalId}",
                userPermissions.Count, principalId);

            if (PermissionMatcher.Match(permission, userPermissions))
            {
                hasPermission = true;
            }
        }

        if (hasPermission)
        {
            if (!string.IsNullOrEmpty(auditPurpose))
            {
                httpContext.Request.Headers["X-Internal-Audit-Purpose"] = auditPurpose;
            }

            _authMetrics?.RecordSuccess(permission);
            return true;
        }

        _authMetrics?.RecordFailure(permission, "Permission denied");
        return false;
    }

    private string ResolveResourcePath(string template, RouteValueDictionary routeValues)
    {
        var path = template;
        foreach (var rv in routeValues)
        {
            path = path.Replace($"{{{rv.Key}}}", rv.Value?.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        return path;
    }
}
