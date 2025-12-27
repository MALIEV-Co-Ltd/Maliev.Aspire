using System.Security.Claims;
using Maliev.Aspire.ServiceDefaults.IAM;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Maliev.Aspire.ServiceDefaults.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PermissionAuthorizationHandler> _logger;
    private readonly IAuthMetrics? _authMetrics;

    public PermissionAuthorizationHandler(
        IServiceProvider serviceProvider,
        ILogger<PermissionAuthorizationHandler> logger,
        IAuthMetrics? authMetrics = null)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _authMetrics = authMetrics;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        var succeeded = await CheckRequirementAsync(
            context,
            requirement.Permission,
            requirement.ResourcePathTemplate,
            requirement.RequireLiveCheck,
            requirement.PreValidateModel,
            requirement.IsCritical,
            requirement.AuditPurpose);

        if (succeeded)
        {
            context.Succeed(requirement);
        }
        else
        {
            // Veto Behavior Documentation
            // ===========================
            // We explicitly call context.Fail() to veto the authorization request.
            // This is INTENTIONAL for permission-based authorization because:
            //
            // 1. Permission requirements are MANDATORY - lack of permission = explicit denial
            // 2. Prevents other authorization handlers from incorrectly satisfying this requirement
            // 3. Ensures permission checks cannot be bypassed by fallback handlers
            //
            // Alternative Considered: Just not calling Succeed()
            // - This would leave the requirement unsatisfied but not explicitly failed
            // - Other handlers could potentially satisfy it (security risk for permissions)
            // - ASP.NET Core would still deny if no handler succeeds, but less explicit
            //
            // Multiple Auth Schemes Compatibility:
            // - Unlikely to cause issues because permission requirements are specific
            // - If using multiple schemes, ensure each scheme has appropriate requirements
            // - Veto is appropriate for permission-based access control (PBAC)
            //
            // Reviewed: 2025-12-26 - Veto behavior is CORRECT for permission authorization
            context.Fail();
        }
    }

    private async Task<bool> CheckRequirementAsync(
        AuthorizationHandlerContext context,
        string permission,
        string? resourcePathTemplate,
        bool requireLiveCheck,
        bool preValidateModel,
        bool isCritical,
        string? auditPurpose)
    {
        var httpContext = _serviceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext;
        if (httpContext == null)
        {
            return false;
        }

        var user = context.User;
        if (user.Identity == null || !user.Identity.IsAuthenticated)
        {
            return false;
        }

        var principalId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? user.FindFirst("sub")?.Value;

        // Security fix: Use Distinct() to prevent duplicate permission processing
        // Concatenating claims from both "permissions" and "permission" types could result in duplicates
        // if a token contains both claim types or multiple claims of the same type
        var userPermissions = user.Claims
            .Where(c => c.Type == "permissions" || c.Type == "permission")
            .Select(c => c.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (string.IsNullOrEmpty(principalId))
        {
            return false;
        }

        bool hasPermission = false;
        string? resourcePath = null;

        if (PermissionMatcher.Match(permission, userPermissions))
        {
            hasPermission = true;
        }
        else
        {
            // Fallback to IAM if enabled and required
            var config = _serviceProvider.GetRequiredService<IConfiguration>();
            var resourceScopedEnabled = config.GetValue<bool>("Features:ResourceScopedAuthEnabled", false);
            var needsResourceCheck = !string.IsNullOrEmpty(resourcePathTemplate) && resourceScopedEnabled;
            var shouldCallIam = requireLiveCheck || needsResourceCheck;

            if (shouldCallIam)
            {
                var iamClient = _serviceProvider.GetService<IIamServiceClient>();
                if (iamClient != null)
                {
                    if (!string.IsNullOrEmpty(resourcePathTemplate))
                    {
                        resourcePath = ResolveResourcePath(resourcePathTemplate, httpContext.GetRouteData().Values);
                    }

                    try
                    {
                        hasPermission = await iamClient.CheckPermissionAsync(principalId, permission, resourcePath);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "IAM service call failed");
                        if (config.GetValue<bool>("Features:FailOpenOnIAMError", false))
                        {
                            hasPermission = true;
                        }
                    }
                }
            }
        }

        if (hasPermission)
        {
            if (isCritical)
            {
                LogEnhancedAudit(httpContext, user, permission, auditPurpose);
            }

            if (!string.IsNullOrEmpty(auditPurpose))
            {
                // Security: Use internal header prefix to prevent client spoofing
                // Client-provided "X-Audit-Purpose" headers are ignored; only server-side
                // attribute-based audit purposes are trusted
                httpContext.Request.Headers["X-Internal-Audit-Purpose"] = auditPurpose;
            }

            _authMetrics?.RecordSuccess(permission);
            return true;
        }

        _authMetrics?.RecordFailure(permission, "Insufficient permissions");
        return false;
    }

    private void LogEnhancedAudit(HttpContext context, ClaimsPrincipal user, string permission, string? auditPurpose)
    {
        var principalId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "unknown";
        var clientId = user.FindFirst("client_id")?.Value ?? "unknown";
        var ipAddress = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        _logger.LogInformation(
            "CRITICAL_PERMISSION_CHECK: PrincipalId={PrincipalId}, ClientId={ClientId}, IPAddress={IPAddress}, PermissionId={PermissionId}, Purpose={Purpose}",
            principalId, clientId, ipAddress, permission, auditPurpose ?? "not specified");
    }

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
}
