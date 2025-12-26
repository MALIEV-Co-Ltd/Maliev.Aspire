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

public class PermissionAuthorizationHandler : IAuthorizationHandler
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PermissionAuthorizationHandler> _logger;
    private readonly IAuthMetrics? _authMetrics;

    public PermissionAuthorizationHandler(
        IServiceProvider serviceProvider, 
        ILogger<PermissionAuthorizationHandler> logger,
        IAuthMetrics? authMetrics = null)
    {
        Console.WriteLine("DEBUG AUTH: PermissionAuthorizationHandler instantiated");
        _serviceProvider = serviceProvider;
        _logger = logger;
        _authMetrics = authMetrics;
    }

    public async Task HandleAsync(AuthorizationHandlerContext context)
    {
        var pending = context.PendingRequirements.ToList();
        Console.WriteLine($"DEBUG AUTH: HandleAsync called with {pending.Count} pending requirements");

        foreach (var requirement in pending)
        {
            if (requirement is PermissionRequirement permReq)
            {
                Console.WriteLine($"DEBUG AUTH: Processing PermissionRequirement for {permReq.Permission}");
                
                var succeeded = await CheckRequirementAsync(
                    context, 
                    permReq.Permission, 
                    permReq.ResourcePathTemplate, 
                    permReq.RequireLiveCheck, 
                    permReq.PreValidateModel);
                
                if (succeeded)
                {
                    Console.WriteLine($"DEBUG AUTH: Requirement SUCCEEDED for {permReq.Permission}. Marking requirement as satisfied.");
                    context.Succeed(requirement);
                }
                else
                {
                    Console.WriteLine($"DEBUG AUTH: Requirement FAILED for {permReq.Permission}.");
                    // We call context.Fail() here to ensure the context is marked as failed
                    // if this MANDATORY permission requirement is not met.
                    context.Fail();
                }
            }
            else
            {
                Console.WriteLine($"DEBUG AUTH: Skipping non-permission requirement: {requirement.GetType().FullName}");
            }
        }
    }

    private async Task<bool> CheckRequirementAsync(AuthorizationHandlerContext context, string permission, string? resourcePathTemplate, bool requireLiveCheck, bool preValidateModel)
    {
        var httpContext = _serviceProvider.GetRequiredService<IHttpContextAccessor>().HttpContext;
        if (httpContext == null)
        {
            Console.WriteLine("DEBUG AUTH: HttpContext is null");
            return false;
        }

        var user = context.User;
        if (user.Identity == null || !user.Identity.IsAuthenticated)
        {
            Console.WriteLine($"DEBUG AUTH: User not authenticated.");
            return false;
        }

        var config = _serviceProvider.GetRequiredService<IConfiguration>();
        var iamEnabled = config.GetValue<bool>("Features:PermissionBasedAuthEnabled", true);

        if (!iamEnabled)
        {
            return true;
        }

        var requiredPermissionWithPrefix = permission.StartsWith("Permission:", StringComparison.OrdinalIgnoreCase)
            ? permission
            : $"Permission:{permission}";

        var principalId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value 
            ?? user.FindFirst("sub")?.Value;

        Console.WriteLine($"DEBUG AUTH: principalId={principalId}, requiredPermission={requiredPermissionWithPrefix}");

        if (string.IsNullOrEmpty(principalId))
        {
            Console.WriteLine("DEBUG AUTH: No principal ID found in claims");
            return false;
        }

        // Fast path: Check JWT-embedded permissions
        var userPermissions = user.FindAll("permissions")
            .Concat(user.FindAll("permission"))
            .Select(c => c.Value)
            .ToList();

        Console.WriteLine($"DEBUG AUTH: Found {userPermissions.Count} user permissions");

        if (PermissionMatcher.Match(requiredPermissionWithPrefix, userPermissions))
        {
            Console.WriteLine($"DEBUG AUTH: Match SUCCEEDED for {requiredPermissionWithPrefix}");
            _authMetrics?.RecordSuccess(permission);
            return true;
        }

        Console.WriteLine($"DEBUG AUTH: Match FAILED for {requiredPermissionWithPrefix}");

        // Fallback to IAM if enabled and required
        var resourceScopedEnabled = config.GetValue<bool>("Features:ResourceScopedAuthEnabled", false);
        var needsResourceCheck = !string.IsNullOrEmpty(resourcePathTemplate) && resourceScopedEnabled;
        var shouldCallIam = requireLiveCheck || needsResourceCheck;

        if (shouldCallIam)
        {
            var iamClient = _serviceProvider.GetService<IIamServiceClient>();
            if (iamClient != null)
            {
                string? resourcePath = null;
                if (!string.IsNullOrEmpty(resourcePathTemplate))
                {
                    resourcePath = ResolveResourcePath(resourcePathTemplate, httpContext.GetRouteData().Values);
                }

                try
                {
                    Console.WriteLine($"DEBUG AUTH: Calling IAM for {requiredPermissionWithPrefix} on {resourcePath ?? "none"}");
                    var hasPermission = await iamClient.CheckPermissionAsync(principalId, requiredPermissionWithPrefix, resourcePath);
                    if (hasPermission)
                    {
                        Console.WriteLine($"DEBUG AUTH: IAM Match SUCCEEDED for {requiredPermissionWithPrefix}");
                        _authMetrics?.RecordSuccess(permission);
                        return true;
                    }
                    Console.WriteLine($"DEBUG AUTH: IAM Match FAILED for {requiredPermissionWithPrefix}");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "IAM service call failed");
                    if (config.GetValue<bool>("Features:FailOpenOnIAMError", false))
                    {
                        return true;
                    }
                }
            }
        }

        _authMetrics?.RecordFailure(permission, "Insufficient permissions");
        return false;
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
