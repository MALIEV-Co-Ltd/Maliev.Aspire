using Maliev.Aspire.ServiceDefaults.IAM;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Maliev.Aspire.Tests.Unit;

/// <summary>
/// Regression tests for shared IAM service-client behavior.
/// </summary>
public sealed class IamServiceClientTests
{
    /// <summary>
    /// High-volume authorization success-path logs should stay below Information level.
    /// </summary>
    [Fact]
    public void PermissionAuthorizationHandler_UsesDebugForPerRequestPermissionLogs()
    {
        var source = File.ReadAllText(FindRepoFile(
            "Maliev.Aspire.ServiceDefaults",
            "Authorization",
            "PermissionAuthorizationHandler.cs"));

        Assert.Contains("_logger.LogDebug(\"Checking permission {Permission}", source, StringComparison.Ordinal);
        Assert.Contains("_logger.LogDebug(\"JWT contains wildcard permission", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_logger.LogInformation(\"Checking permission {Permission}", source, StringComparison.Ordinal);
        Assert.DoesNotContain("_logger.LogInformation(\"JWT contains wildcard permission", source, StringComparison.Ordinal);
    }

    /// <summary>
    /// Concurrent identical permission checks should share one upstream IAM request.
    /// </summary>
    [Fact]
    public async Task CheckPermissionAsync_ConcurrentIdenticalChecks_UsesSingleUpstreamRequest()
    {
        var handler = new CountingPermissionHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://iam.test")
        };

        var client = new IamServiceClient(
            new StaticHttpClientFactory(httpClient),
            NullLogger<IamServiceClient>.Instance,
            new TestHostEnvironment());

        var principalId = $"system:service:test-{Guid.NewGuid():N}";
        var tasks = Enumerable.Range(0, 32)
            .Select(_ => client.CheckPermissionAsync(principalId, "material.materials.read", "global"))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        Assert.All(results, Assert.True);
        Assert.Equal(1, handler.RequestCount);
    }

    /// <summary>
    /// Forced-live checks must bypass a previously cached standard result and request an authoritative IAM read.
    /// </summary>
    [Fact]
    public async Task CheckPermissionLiveAsync_CachedStandardResult_SendsBypassRequest()
    {
        var handler = new LivePermissionHandler();
        using var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://iam.test")
        };
        var client = new IamServiceClient(
            new StaticHttpClientFactory(httpClient),
            NullLogger<IamServiceClient>.Instance,
            new TestHostEnvironment());
        var principalId = $"user-{Guid.NewGuid():N}";

        var standardResult = await client.CheckPermissionAsync(
            principalId,
            "project.projects.read",
            "projects/project-123");
        var liveResult = await client.CheckPermissionLiveAsync(
            principalId,
            "project.projects.read",
            "projects/project-123");

        Assert.True(standardResult);
        Assert.False(liveResult);
        Assert.Equal([false, true], handler.BypassCacheValues);
    }

    private sealed class CountingPermissionHandler : HttpMessageHandler
    {
        private int _requestCount;

        public int RequestCount => Volatile.Read(ref _requestCount);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _requestCount);
            await Task.Delay(100, cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    PrincipalId = "system:service:test",
                    PermissionId = "material.materials.read",
                    Allowed = true,
                    ResourcePath = "global",
                    FromCache = false,
                    LatencyMs = 1
                })
            };
        }
    }

    private sealed class LivePermissionHandler : HttpMessageHandler
    {
        public List<bool> BypassCacheValues { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var payload = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var bypassCache = payload.TryGetProperty("bypassCache", out var value) && value.GetBoolean();
            BypassCacheValues.Add(bypassCache);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    PrincipalId = "user-123",
                    PermissionId = "project.projects.read",
                    Allowed = !bypassCache,
                    ResourcePath = "projects/project-123",
                    FromCache = false,
                    LatencyMs = 1
                })
            };
        }
    }

    private sealed class StaticHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _httpClient;

        public StaticHttpClientFactory(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public HttpClient CreateClient(string name) => _httpClient;
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "Maliev.Aspire.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static string FindRepoFile(params string[] relativeParts)
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);
        while (current is not null)
        {
            var candidate = Path.Combine(new[] { current.FullName }.Concat(relativeParts).ToArray());
            if (File.Exists(candidate))
            {
                return candidate;
            }

            current = current.Parent;
        }

        throw new FileNotFoundException("Could not find repository file.", Path.Combine(relativeParts));
    }
}
