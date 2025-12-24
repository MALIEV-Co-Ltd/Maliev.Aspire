using System.Security.Claims;
using Maliev.Aspire.ServiceDefaults.IAM;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Maliev.Aspire.ServiceDefaults.Authorization;

/// <summary>
/// Declarative permission-based authorization for controller actions.
/// Supports both global and resource-scoped permissions with automatic route parameter resolution.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class RequirePermissionAttribute : AuthorizeAttribute, IAsyncAuthorizationFilter
{
    /// <summary>
    /// Gets the required permission string.
    /// </summary>
    public string Permission { get; }

    /// <summary>
    /// Gets or sets the resource path template for scoped authorization.
    /// Template supports route parameter substitution, e.g., "customers/{customerId}/orders/{orderId}".
    /// If specified, the attribute will resolve the actual resource path from route values and call IAM service.
    /// </summary>
    public string? ResourcePathTemplate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether to bypass JWT cache and always call IAM service.
    /// Use this for high-security operations requiring real-time permission checks.
    /// Default is false (uses JWT-embedded permissions for performance).
    /// </summary>
    public bool RequireLiveCheck { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this permission check is critical and should be audited.
    /// </summary>
    public bool IsCritical { get; set; }
    
    /// <summary>
    /// Gets or sets the purpose of the access for audit logging.
    /// </summary>
    public string? AuditPurpose { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequirePermissionAttribute"/> class.
    /// </summary>
    /// <param name="permission">The required permission string in 'service.resource.action' format.</param>
    public RequirePermissionAttribute(string permission)
    {
        Permission = permission ?? throw new ArgumentNullException(nameof(permission));

        if (!IsValidPermissionFormat(permission))
        {
            throw new ArgumentException(
                $"Invalid permission format '{permission}'. Expected format: 'service.resource.action'",
                nameof(permission));
        }
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context == null) throw new ArgumentNullException(nameof(context));

        var user = context.HttpContext.User;
        var logger = context.HttpContext.RequestServices.GetService<ILogger<RequirePermissionAttribute>>();
        
        if (!user.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new ChallengeResult();
            return;
        }

        var config = context.HttpContext.RequestServices.GetService<IConfiguration>();
        var iamEnabled = config?.GetValue<bool>("Features:PermissionBasedAuthEnabled") ?? true;

        if (!iamEnabled)
        {
            return;
        }

        var principalId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? user.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(principalId))
        {
            logger?.LogWarning("No principal ID found in claims for permission check");
            context.Result = new ForbidResult();
            return;
        }

        // Determine if we need resource-scoped check
        var resourceScopedEnabled = config?.GetValue<bool>("Features:ResourceScopedAuthEnabled") ?? false;
        var needsResourceCheck = !string.IsNullOrEmpty(ResourcePathTemplate) && resourceScopedEnabled;
        var shouldCallIam = RequireLiveCheck || needsResourceCheck;

        bool hasPermission;

        if (shouldCallIam)
        {
            // Call IAM service for live check (resource-scoped or forced live check)
            hasPermission = await CheckPermissionViaIamAsync(context, principalId, logger);
        }
        else
        {
            // Fast path: Check JWT-embedded permissions
            var permissions = user.FindAll("permissions")
                .Select(c => c.Value)
                .ToList();

            hasPermission = PermissionMatcher.Match(Permission, permissions);

            logger?.LogDebug(
                "JWT permission check: Principal={PrincipalId}, Permission={Permission}, Result={Result}",
                principalId, Permission, hasPermission);
        }

        if (!hasPermission)
        {
            logger?.LogWarning(
                "Authorization failed: Principal={PrincipalId}, Permission={Permission}, ResourceTemplate={ResourceTemplate}",
                principalId, Permission, ResourcePathTemplate ?? "global");
            context.Result = new ForbidResult();
            return;
        }

        if (IsCritical)
        {
            LogEnhancedAudit(context, user);
        }

        if (!string.IsNullOrEmpty(AuditPurpose))
        {
            context.HttpContext.Request.Headers["X-Audit-Purpose"] = AuditPurpose;
        }
    }

    private async Task<bool> CheckPermissionViaIamAsync(
        AuthorizationFilterContext context, 
        string principalId, 
        ILogger<RequirePermissionAttribute>? logger)
    {
        var iamClient = context.HttpContext.RequestServices.GetService<IIamServiceClient>();
        
        if (iamClient == null)
        {
            logger?.LogWarning(
                "IIamServiceClient not registered but resource-scoped check or live check was requested. " +
                "Falling back to JWT permissions. Register IIamServiceClient in DI to enable this feature.");
            
            // Graceful degradation: fall back to JWT
            var permissions = context.HttpContext.User.FindAll("permissions")
                .Select(c => c.Value)
                .ToList();
            return PermissionMatcher.Match(Permission, permissions);
        }

        // Resolve resource path from route values if template is specified
        string? resourcePath = null;
        if (!string.IsNullOrEmpty(ResourcePathTemplate))
        {
            resourcePath = ResolveResourcePath(ResourcePathTemplate, context.RouteData.Values);
            logger?.LogDebug(
                "Resolved resource path: Template={Template}, Path={Path}",
                ResourcePathTemplate, resourcePath);
        }

        try
        {
            var hasPermission = await iamClient.CheckPermissionAsync(
                principalId, 
                Permission, 
                resourcePath, 
                context.HttpContext.RequestAborted);

            logger?.LogInformation(
                "IAM permission check: Principal={PrincipalId}, Permission={Permission}, ResourcePath={ResourcePath}, Result={Result}",
                principalId, Permission, resourcePath ?? "global", hasPermission);

            return hasPermission;
        }
        catch (Exception ex)
        {
            logger?.LogError(ex, 
                "IAM service call failed for Principal={PrincipalId}, Permission={Permission}",
                principalId, Permission);

            // Fail-secure by default (return 503 on IAM failure)
            var config = context.HttpContext.RequestServices.GetService<IConfiguration>();
            var failOpen = config?.GetValue<bool>("Features:FailOpenOnIAMError") ?? false;

            if (failOpen)
            {
                logger?.LogWarning("IAM error with fail-open enabled, allowing request");
                return true;
            }

            context.Result = new ObjectResult("Authorization service unavailable")
            {
                StatusCode = 503
            };
            return false;
        }
    }

    /// <summary>
    /// Resolves a resource path template by substituting route parameter values.
    /// Example: "customers/{customerId}/orders/{orderId}" with route values {customerId=123, orderId=456}
    /// produces "customers/123/orders/456".
    /// </summary>
    private static string ResolveResourcePath(string template, RouteValueDictionary routeValues)
    {
        var result = template;
        foreach (var (key, value) in routeValues)
        {
            var placeholder = $"{{{key}}}";
            if (result.Contains(placeholder, StringComparison.OrdinalIgnoreCase))
            {
                result = result.Replace(placeholder, value?.ToString() ?? "", StringComparison.OrdinalIgnoreCase);
            }
        }
        return result;
    }

    private void LogEnhancedAudit(AuthorizationFilterContext context, ClaimsPrincipal user)
    {
        var logger = context.HttpContext.RequestServices.GetService<ILogger<RequirePermissionAttribute>>();
        if (logger != null)
        {
            var principalId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
            var clientId = user.FindFirst("client_id")?.Value ?? "unknown";
            var ipAddress = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

            logger.LogInformation(
                "CRITICAL_PERMISSION_CHECK: PrincipalId={PrincipalId}, ClientId={ClientId}, IPAddress={IPAddress}, PermissionId={PermissionId}, Purpose={Purpose}",
                principalId, clientId, ipAddress, Permission, AuditPurpose ?? "not specified");
        }
    }

    private static bool IsValidPermissionFormat(string permission)
    {
        var parts = permission.Split('.');
        return parts.Length == 3 && parts.All(p => !string.IsNullOrWhiteSpace(p));
    }
}