using Maliev.Aspire.DatabaseSeeder.Seeding.Services.AuthService;
using Maliev.Aspire.DatabaseSeeder.Seeding.Services.IAMService;
using Maliev.Aspire.DatabaseSeeder.Seeding.Services.Shared;
using Maliev.AuthService.Domain.Entities;
using Maliev.AuthService.Infrastructure.DbContexts;
using Maliev.IAMService.Application.Services;
using Maliev.IAMService.Application.Workloads;
using Maliev.IAMService.Domain.Entities;
using Maliev.IAMService.Infrastructure.Persistence;
using Maliev.IAMService.Infrastructure.Workloads;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Testcontainers.PostgreSql;

namespace Maliev.Aspire.Tests.Integration;

/// <summary>
/// Real-PostgreSQL verification for the local IAM/Auth identity seed boundary.
/// </summary>
public sealed class LocalServiceIdentitySeederIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _iamPostgres = CreatePostgres("aspire_local_iam");
    private readonly PostgreSqlContainer _authPostgres = CreatePostgres("aspire_local_auth");

    /// <inheritdoc />
    public async Task InitializeAsync() =>
        await Task.WhenAll(_iamPostgres.StartAsync(), _authPostgres.StartAsync());

    /// <inheritdoc />
    public async Task DisposeAsync()
    {
        await _authPostgres.DisposeAsync();
        await _iamPostgres.DisposeAsync();
    }

    /// <summary>
    /// The full seed path must persist exact authority, rotate fresh verifiers, and reject IAM drift.
    /// </summary>
    [Fact]
    public async Task Seed_ExactLocalIdentity_RotatesVerifierAndRejectsAuthorityDrift()
    {
        var firstMaterial = LocalServiceIdentitySeedMaterial.Create();
        await using var iamContext = CreateIamContext();
        await RunIamSeederAsync(iamContext, firstMaterial.SecretHash);

        var actor = await iamContext.Principals.AsNoTracking().SingleAsync(
            principal => principal.PrincipalId == LocalServiceIdentitySeedOptions.ProvisionerPrincipalId);
        Assert.Equal("user", actor.PrincipalType);
        Assert.True(actor.IsActive);
        Assert.Null(actor.WorkloadId);

        var actorBinding = await iamContext.PrincipalRoleBindings.AsNoTracking().SingleAsync(
            binding => binding.PrincipalId == actor.PrincipalId);
        Assert.Equal(LocalServiceIdentitySeedOptions.ProvisionerRoleId, actorBinding.RoleId);
        Assert.Null(actorBinding.ResourcePath);
        Assert.Null(actorBinding.ExpiresAt);

        var actorPermissions = await (
            from binding in iamContext.PrincipalRoleBindings
            join rolePermission in iamContext.RolePermissions on binding.RoleId equals rolePermission.RoleId
            where binding.PrincipalId == actor.PrincipalId
            select rolePermission.PermissionId).ToListAsync();
        Assert.Equal([LocalServiceIdentitySeedOptions.ProvisionPermission], actorPermissions);
        Assert.DoesNotContain("*", actorPermissions);
        Assert.DoesNotContain(
            await iamContext.PrincipalRoleBindings.AsNoTracking()
                .Where(binding => binding.PrincipalId == actor.PrincipalId)
                .Select(binding => binding.RoleId)
                .ToListAsync(),
            roleId => roleId.Contains("platform.owner", StringComparison.OrdinalIgnoreCase));

        var workload = await iamContext.Principals.AsNoTracking().SingleAsync(
            principal => principal.WorkloadId == LocalServiceIdentitySeedOptions.WorkloadId);
        var workloadBinding = await iamContext.PrincipalRoleBindings.AsNoTracking().SingleAsync(
            binding => binding.PrincipalId == workload.PrincipalId);
        Assert.Equal(LocalServiceIdentitySeedOptions.RoleId, workloadBinding.RoleId);
        Assert.Null(workloadBinding.ResourcePath);
        Assert.Empty(await iamContext.PrincipalPermissionBindings.AsNoTracking()
            .Where(binding => binding.PrincipalId == workload.PrincipalId)
            .ToListAsync());
        Assert.Empty(await iamContext.ServiceAccountApiKeys.AsNoTracking()
            .Where(key => key.PrincipalId == workload.PrincipalId)
            .ToListAsync());

        await using var authContext = CreateAuthContext();
        await authContext.Database.OpenConnectionAsync();
        await authContext.Database.CloseConnectionAsync();
        await RunAuthSeederAsync(authContext, iamContext, firstMaterial.SecretHash);

        var firstCredential = await authContext.ServiceCredentials.AsNoTracking()
            .Include(credential => credential.Versions)
            .SingleAsync(credential => credential.WorkloadId == LocalServiceIdentitySeedOptions.WorkloadId);
        Assert.Equal(workload.PrincipalId, firstCredential.PrincipalId);
        Assert.Equal(firstMaterial.SecretHash, firstCredential.ClientSecretHash);
        Assert.DoesNotContain(firstMaterial.RawSecret, GetPersistedCredentialStrings(firstCredential));
        var firstVersion = Assert.Single(firstCredential.Versions);
        Assert.Equal(ServiceCredentialVersionStatus.Active, firstVersion.Status);
        Assert.Equal(firstMaterial.SecretHash, firstVersion.SecretHash);

        // Replaying the same AppHost material is idempotent; a new AppHost material rotates it.
        await RunIamSeederAsync(iamContext, firstMaterial.SecretHash);
        await RunAuthSeederAsync(authContext, iamContext, firstMaterial.SecretHash);
        Assert.Equal(1, await authContext.ServiceCredentialVersions.CountAsync());

        var secondMaterial = LocalServiceIdentitySeedMaterial.Create();
        await RunAuthSeederAsync(authContext, iamContext, secondMaterial.SecretHash);
        var versions = await authContext.ServiceCredentialVersions.AsNoTracking()
            .OrderBy(version => version.Version)
            .ToListAsync();
        Assert.Equal(2, versions.Count);
        Assert.Equal(ServiceCredentialVersionStatus.Revoked, versions[0].Status);
        Assert.NotNull(versions[0].RevokedAt);
        Assert.Equal(ServiceCredentialVersionStatus.Active, versions[1].Status);
        Assert.Equal(secondMaterial.SecretHash, versions[1].SecretHash);
        Assert.DoesNotContain(secondMaterial.RawSecret, versions.Select(version => version.SecretHash));

        var thirdMaterial = LocalServiceIdentitySeedMaterial.Create();
        await using (var failingAuthContext = CreateAuthContext(new ThrowOnSaveChangesInterceptor(2)))
        {
            await Assert.ThrowsAsync<InjectedSeedFailureException>(() =>
                RunAuthSeederAsync(failingAuthContext, iamContext, thirdMaterial.SecretHash));
        }

        await using (var verificationContext = CreateAuthContext())
        {
            var credentialAfterRollback = await verificationContext.ServiceCredentials
                .AsNoTracking()
                .Include(credential => credential.Versions)
                .SingleAsync(credential => credential.WorkloadId == LocalServiceIdentitySeedOptions.WorkloadId);
            Assert.Equal(secondMaterial.SecretHash, credentialAfterRollback.ClientSecretHash);
            Assert.Equal(2, credentialAfterRollback.Versions.Count);
            Assert.Equal(
                secondMaterial.SecretHash,
                Assert.Single(
                    credentialAfterRollback.Versions,
                    version => version.Status == ServiceCredentialVersionStatus.Active).SecretHash);
        }

        iamContext.PrincipalPermissionBindings.Add(new PrincipalPermissionBinding
        {
            BindingId = Guid.NewGuid(),
            PrincipalId = workload.PrincipalId,
            PermissionId = LocalServiceIdentitySeedOptions.WorkloadPermission,
            ResourcePath = null,
            GrantedAt = DateTime.UtcNow
        });
        await iamContext.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RunAuthSeederAsync(authContext, iamContext, secondMaterial.SecretHash));

        iamContext.ChangeTracker.Clear();
        var provisionerActor = await iamContext.Principals.SingleAsync(
            principal => principal.PrincipalId == LocalServiceIdentitySeedOptions.ProvisionerPrincipalId);
        provisionerActor.IsActive = false;
        iamContext.PrincipalPermissionBindings.Add(new PrincipalPermissionBinding
        {
            BindingId = Guid.NewGuid(),
            PrincipalId = provisionerActor.PrincipalId,
            PermissionId = LocalServiceIdentitySeedOptions.ProvisionPermission,
            ResourcePath = null,
            GrantedAt = DateTime.UtcNow
        });
        await iamContext.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RunIamSeederAsync(iamContext, secondMaterial.SecretHash));
        iamContext.ChangeTracker.Clear();
        Assert.False(await iamContext.Principals
            .Where(principal => principal.PrincipalId == provisionerActor.PrincipalId)
            .Select(principal => principal.IsActive)
            .SingleAsync());
    }

    private static PostgreSqlContainer CreatePostgres(string database) =>
        new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase(database)
            .WithUsername("test_user")
            .WithPassword("test_password")
            .WithPortBinding(5432, true)
            // The fixture disposes both containers explicitly; avoid requiring the
            // Testcontainers resource-reaper sidecar on constrained local runners.
            .WithCleanUp(false)
            .Build();

    private IAMDbContext CreateIamContext() => new(
        new DbContextOptionsBuilder<IAMDbContext>()
            .UseNpgsql(_iamPostgres.GetConnectionString())
            .Options);

    private AuthDbContext CreateAuthContext(params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseNpgsql(_authPostgres.GetConnectionString());
        if (interceptors.Length > 0)
        {
            options.AddInterceptors(interceptors);
        }

        return new AuthDbContext(options.Options);
    }

    private static async Task RunIamSeederAsync(IAMDbContext context, string secretHash)
    {
        var cache = new Mock<ICacheService>();
        cache.Setup(service => service.RemoveByPrefixAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var provisioner = new WorkloadPrincipalProvisioner(
            context,
            WorkloadAccessProfileCatalog.Default,
            cache.Object);
        var seeder = new IAMDatabaseSeeder(
            context,
            NullLogger<IAMDatabaseSeeder>.Instance,
            BuildConfiguration(secretHash),
            BuildEnvironment(),
            provisioner);
        await seeder.ExecuteAsync();
    }

    private static async Task RunAuthSeederAsync(
        AuthDbContext authContext,
        IAMDbContext iamContext,
        string secretHash)
    {
        var seeder = new AuthServiceIdentityDatabaseSeeder(
            authContext,
            iamContext,
            NullLogger<AuthServiceIdentityDatabaseSeeder>.Instance,
            BuildConfiguration(secretHash),
            BuildEnvironment());
        await seeder.ExecuteAsync();
    }

    private static IConfiguration BuildConfiguration(string secretHash) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AspireLocalServiceIdentity:Enabled"] = "true",
                ["AspireLocalServiceIdentity:SecretHash"] = secretHash,
                ["AspireTestAdmin:Enabled"] = "false"
            })
            .Build();

    private static IHostEnvironment BuildEnvironment()
    {
        var environment = new Mock<IHostEnvironment>();
        environment.SetupGet(value => value.EnvironmentName).Returns("Testing");
        return environment.Object;
    }

    private static IEnumerable<string> GetPersistedCredentialStrings(ServiceCredential credential)
    {
        yield return credential.ClientId;
        yield return credential.ClientSecretHash;
        yield return credential.ServiceName;
        yield return credential.WorkloadId ?? string.Empty;
        yield return credential.RoleId ?? string.Empty;
        foreach (var version in credential.Versions)
        {
            yield return version.SecretHash;
        }
    }

    private sealed class ThrowOnSaveChangesInterceptor(int saveNumber) : SaveChangesInterceptor
    {
        private int _saveCount;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _saveCount) == saveNumber)
            {
                throw new InjectedSeedFailureException();
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class InjectedSeedFailureException : Exception;
}
