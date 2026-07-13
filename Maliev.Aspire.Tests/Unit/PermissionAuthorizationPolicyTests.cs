using System.Security.Claims;
using Maliev.Aspire.ServiceDefaults.Authorization;
using Maliev.Aspire.ServiceDefaults.IAM;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Maliev.Aspire.Tests.Unit;

/// <summary>
/// Verifies shared permission attributes preserve their enforcement metadata.
/// </summary>
public sealed class PermissionAuthorizationPolicyTests
{
    /// <summary>
    /// Resource-scoped and forced-live attribute options must survive dynamic policy construction.
    /// </summary>
    [Fact]
    public async Task GetPolicyAsync_ResourceScopedLiveAttribute_PreservesRequirementMetadata()
    {
        var attribute = new RequirePermissionAttribute("project.projects.read")
        {
            ResourcePathTemplate = "customers/{customerId}/projects/{projectId}",
            RequireLiveCheck = true,
            PreValidateModel = true,
            IsCritical = true,
            AuditPurpose = "Project access review"
        };
        var provider = new PermissionAuthorizationPolicyProvider(
            Options.Create(new AuthorizationOptions()));

        var policy = await provider.GetPolicyAsync(attribute.Policy!);

        Assert.NotNull(policy);
        var requirement = Assert.Single(policy.Requirements.OfType<PermissionRequirement>());
        Assert.Equal("project.projects.read", requirement.Permission);
        Assert.Equal("customers/{customerId}/projects/{projectId}", requirement.ResourcePathTemplate);
        Assert.True(requirement.RequireLiveCheck);
        Assert.True(requirement.PreValidateModel);
        Assert.True(requirement.IsCritical);
        Assert.Equal("Project access review", requirement.AuditPurpose);
    }

    /// <summary>
    /// A prior claim-based success must not satisfy a forced-live requirement in the same request.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ForcedLiveRequirement_DoesNotReuseClaimFallbackResult()
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        var httpContext = new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = httpContext };
        var handler = new PermissionAuthorizationHandler(
            services,
            accessor,
            NullLogger<PermissionAuthorizationHandler>.Instance);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "user-123"),
            new Claim("permissions", "project.projects.read")
        ], "test"));

        var standardRequirement = new PermissionRequirement("project.projects.read");
        var standardContext = new AuthorizationHandlerContext(
            [standardRequirement],
            principal,
            httpContext);
        await handler.HandleAsync(standardContext);
        Assert.True(standardContext.HasSucceeded);

        var liveRequirement = new PermissionRequirement(
            "project.projects.read",
            requireLiveCheck: true);
        var liveContext = new AuthorizationHandlerContext(
            [liveRequirement],
            principal,
            httpContext);

        await handler.HandleAsync(liveContext);

        Assert.False(liveContext.HasSucceeded);
    }

    /// <summary>
    /// A client that has not implemented authoritative checking must deny forced-live access.
    /// </summary>
    [Fact]
    public async Task HandleAsync_ForcedLiveRequirement_LegacyClientFailsClosed()
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        var httpContext = new DefaultHttpContext();
        var handler = new PermissionAuthorizationHandler(
            services,
            new HttpContextAccessor { HttpContext = httpContext },
            NullLogger<PermissionAuthorizationHandler>.Instance,
            new LegacyIamClient());
        var requirement = new PermissionRequirement(
            "project.projects.read",
            requireLiveCheck: true);
        var context = new AuthorizationHandlerContext(
            [requirement],
            CreatePrincipal(),
            httpContext);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    /// <summary>
    /// Missing route values must deny access without sending a malformed resource path to IAM.
    /// </summary>
    [Fact]
    public async Task HandleAsync_UnresolvedResourceTemplate_DeniesWithoutCallingIam()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Features:ResourceScopedAuthEnabled"] = "true"
            })
            .Build();
        await using var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .BuildServiceProvider();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.RouteValues = new RouteValueDictionary
        {
            ["customerId"] = "customer-123"
        };
        var iamClient = new LegacyIamClient();
        var handler = new PermissionAuthorizationHandler(
            services,
            new HttpContextAccessor { HttpContext = httpContext },
            NullLogger<PermissionAuthorizationHandler>.Instance,
            iamClient);
        var requirement = new PermissionRequirement(
            "project.projects.read",
            "customers/{customerId}/projects/{projectId}");
        var context = new AuthorizationHandlerContext(
            [requirement],
            CreatePrincipal(),
            httpContext);

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
        Assert.Equal(0, iamClient.StandardChecks);
    }

    private static ClaimsPrincipal CreatePrincipal() => new(new ClaimsIdentity(
    [
        new Claim("sub", "user-123")
    ], "test"));

    private sealed class LegacyIamClient : IIamServiceClient
    {
        public int StandardChecks { get; private set; }

        public Task<IEnumerable<string>> GetUserPermissionsAsync(
            string userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<string>>([]);

        public Task<bool> CheckPermissionAsync(
            string principalId,
            string permissionId,
            string? resourcePath = null,
            CancellationToken cancellationToken = default)
        {
            StandardChecks++;
            return Task.FromResult(true);
        }

        public Task<Dictionary<string, bool>> CheckPermissionsAsync(
            string principalId,
            IEnumerable<PermissionCheckRequest> requests,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new Dictionary<string, bool>());

        public Task<IEnumerable<string>> GetAuthorizedResourcesAsync(
            string principalId,
            string permissionId,
            string resourceType,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<string>>([]);
    }
}
