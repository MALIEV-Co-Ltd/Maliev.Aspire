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
    /// Forced-live permission checks must observe and propagate request cancellation without caching a denial.
    /// </summary>
    [Fact]
    public async Task HandleAsync_LiveIamCanceledByRequest_PropagatesWithoutDenialSideEffects()
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        using var requestCancellation = new CancellationTokenSource();
        var httpContext = new DefaultHttpContext
        {
            RequestAborted = requestCancellation.Token
        };
        var iamStarted = new TaskCompletionSource<CancellationToken>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var iamCompletion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var liveCallCount = 0;
        var iamClient = new CoordinatedIamClient
        {
            LiveCheck = (_, _, _, cancellationToken) =>
            {
                if (Interlocked.Increment(ref liveCallCount) > 1)
                {
                    return Task.FromResult(true);
                }

                iamStarted.TrySetResult(cancellationToken);
                cancellationToken.Register(
                    () => iamCompletion.TrySetCanceled(cancellationToken));
                return iamCompletion.Task;
            }
        };
        var metrics = new RecordingAuthMetrics();
        var handler = CreateHandler(services, httpContext, iamClient, metrics);
        var requirement = new PermissionRequirement(
            "project.projects.read",
            requireLiveCheck: true);
        var context = new AuthorizationHandlerContext(
            [requirement],
            CreatePrincipal("live-cancel-user"),
            httpContext);

        var authorization = handler.HandleAsync(context);
        var receivedToken = await iamStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        requestCancellation.Cancel();

        Exception? authorizationException;
        try
        {
            authorizationException = await Record.ExceptionAsync(
                () => authorization.WaitAsync(TimeSpan.FromSeconds(1)));
        }
        finally
        {
            iamCompletion.TrySetResult(false);
        }

        if (!authorization.IsCompleted)
        {
            await authorization.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.IsAssignableFrom<OperationCanceledException>(authorizationException);
        Assert.Equal(requestCancellation.Token, receivedToken);
        Assert.False(context.HasSucceeded);
        Assert.False(context.HasFailed);
        Assert.Empty(httpContext.Items);
        Assert.Equal(0, metrics.Successes);
        Assert.Equal(0, metrics.Failures);

        var reuseHttpContext = new DefaultHttpContext();
        var reuseAuthorizationContext = new AuthorizationHandlerContext(
            [requirement],
            CreatePrincipal("live-cancel-user"),
            reuseHttpContext);
        await CreateHandler(services, reuseHttpContext, iamClient)
            .HandleAsync(reuseAuthorizationContext);

        Assert.True(reuseAuthorizationContext.HasSucceeded);
        Assert.Equal(2, Volatile.Read(ref liveCallCount));
    }

    /// <summary>
    /// Standard permission checks must not use fail-open or claim fallback after request cancellation.
    /// </summary>
    [Fact]
    public async Task HandleAsync_StandardIamCanceledByRequest_DoesNotFallBackOrFailOpen()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Features:FailOpenOnIAMError"] = "true"
            })
            .Build();
        await using var services = new ServiceCollection()
            .AddSingleton<IConfiguration>(configuration)
            .BuildServiceProvider();
        using var requestCancellation = new CancellationTokenSource();
        var httpContext = new DefaultHttpContext
        {
            RequestAborted = requestCancellation.Token
        };
        var iamStarted = new TaskCompletionSource<CancellationToken>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var iamCompletion = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var iamClient = new CoordinatedIamClient
        {
            StandardCheck = (_, _, _, cancellationToken) =>
            {
                iamStarted.TrySetResult(cancellationToken);
                cancellationToken.Register(
                    () => iamCompletion.TrySetCanceled(cancellationToken));
                return iamCompletion.Task;
            }
        };
        var metrics = new RecordingAuthMetrics();
        var handler = CreateHandler(services, httpContext, iamClient, metrics);
        var requirement = new PermissionRequirement("project.projects.read");
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim("sub", "standard-cancel-user"),
            new Claim("permissions", "*")
        ], "test"));
        var context = new AuthorizationHandlerContext([requirement], principal, httpContext);

        var authorization = handler.HandleAsync(context);
        var receivedToken = await iamStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        requestCancellation.Cancel();

        Exception? authorizationException;
        try
        {
            authorizationException = await Record.ExceptionAsync(
                () => authorization.WaitAsync(TimeSpan.FromSeconds(1)));
        }
        finally
        {
            iamCompletion.TrySetResult(false);
        }

        if (!authorization.IsCompleted)
        {
            await authorization.WaitAsync(TimeSpan.FromSeconds(5));
        }

        Assert.IsAssignableFrom<OperationCanceledException>(authorizationException);
        Assert.Equal(requestCancellation.Token, receivedToken);
        Assert.False(context.HasSucceeded);
        Assert.False(context.HasFailed);
        Assert.Empty(httpContext.Items);
        Assert.Equal(0, metrics.Successes);
        Assert.Equal(0, metrics.Failures);
    }

    /// <summary>
    /// Canceling a same-key waiter must not enter IAM or corrupt the shared semaphore.
    /// </summary>
    [Fact]
    public async Task HandleAsync_SameKeyWaiterCanceled_SemaphoreRemainsReusable()
    {
        await using var services = new ServiceCollection().BuildServiceProvider();
        var firstIamStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstIam = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var callCount = 0;
        var iamClient = new CoordinatedIamClient
        {
            LiveCheck = (_, _, _, _) =>
            {
                var currentCall = Interlocked.Increment(ref callCount);
                if (currentCall == 1)
                {
                    firstIamStarted.TrySetResult();
                    return releaseFirstIam.Task;
                }

                return Task.FromResult(true);
            }
        };
        var requirement = new PermissionRequirement(
            "project.projects.read",
            requireLiveCheck: true);
        var firstHttpContext = new DefaultHttpContext();
        var firstAuthorizationContext = new AuthorizationHandlerContext(
            [requirement],
            CreatePrincipal("same-key-user"),
            firstHttpContext);
        var firstAuthorization = CreateHandler(
            services,
            firstHttpContext,
            iamClient).HandleAsync(firstAuthorizationContext);
        await firstIamStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        using var waitingCancellation = new CancellationTokenSource();
        var waitingHttpContext = new DefaultHttpContext
        {
            RequestAborted = waitingCancellation.Token
        };
        var waitingAuthorizationContext = new AuthorizationHandlerContext(
            [requirement],
            CreatePrincipal("same-key-user"),
            waitingHttpContext);
        var waitingAuthorization = CreateHandler(
            services,
            waitingHttpContext,
            iamClient).HandleAsync(waitingAuthorizationContext);

        waitingCancellation.Cancel();
        Exception? waitingException;
        try
        {
            waitingException = await Record.ExceptionAsync(
                () => waitingAuthorization.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.Equal(1, Volatile.Read(ref callCount));
            Assert.Empty(waitingHttpContext.Items);
        }
        finally
        {
            releaseFirstIam.TrySetResult(true);
        }

        await firstAuthorization;
        Assert.True(firstAuthorizationContext.HasSucceeded);
        Assert.IsAssignableFrom<OperationCanceledException>(waitingException);

        var reuseHttpContext = new DefaultHttpContext();
        var reuseAuthorizationContext = new AuthorizationHandlerContext(
            [requirement],
            CreatePrincipal("same-key-user"),
            reuseHttpContext);
        await CreateHandler(services, reuseHttpContext, iamClient)
            .HandleAsync(reuseAuthorizationContext);

        Assert.True(reuseAuthorizationContext.HasSucceeded);
        Assert.Equal(2, Volatile.Read(ref callCount));
    }

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

    private static PermissionAuthorizationHandler CreateHandler(
        IServiceProvider services,
        HttpContext httpContext,
        IIamServiceClient iamClient,
        IAuthMetrics? metrics = null) => new(
            services,
            new HttpContextAccessor { HttpContext = httpContext },
            NullLogger<PermissionAuthorizationHandler>.Instance,
            iamClient,
            metrics);

    private static ClaimsPrincipal CreatePrincipal(string subject = "user-123") => new(new ClaimsIdentity(
    [
        new Claim("sub", subject)
    ], "test"));

    private sealed class RecordingAuthMetrics : IAuthMetrics
    {
        public int Successes { get; private set; }

        public int Failures { get; private set; }

        public void RecordSuccess(string permission) => Successes++;

        public void RecordFailure(string permission, string reason) => Failures++;
    }

    private sealed class CoordinatedIamClient : IIamServiceClient
    {
        public Func<string, string, string?, CancellationToken, Task<bool>>? StandardCheck { get; init; }

        public Func<string, string, string?, CancellationToken, Task<bool>>? LiveCheck { get; init; }

        public Task<IEnumerable<string>> GetUserPermissionsAsync(
            string userId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IEnumerable<string>>([]);

        public Task<bool> CheckPermissionAsync(
            string principalId,
            string permissionId,
            string? resourcePath = null,
            CancellationToken cancellationToken = default) =>
            StandardCheck?.Invoke(principalId, permissionId, resourcePath, cancellationToken)
            ?? Task.FromResult(false);

        public Task<bool> CheckPermissionLiveAsync(
            string principalId,
            string permissionId,
            string? resourcePath = null,
            CancellationToken cancellationToken = default) =>
            LiveCheck?.Invoke(principalId, permissionId, resourcePath, cancellationToken)
            ?? Task.FromResult(false);

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
