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
    /// The full seed path persists both exact profiles, rotates verifiers atomically, and rejects drift.
    /// </summary>
    [Fact]
    public async Task Seed_ExactLocalIdentityCatalog_IsIdempotentAndRejectsAuthorityDrift()
    {
        var firstMaterials = LocalServiceIdentitySeedMaterial.CreateCatalog();
        await using var iamContext = CreateIamContext();
        await RunIamSeederAsync(iamContext, firstMaterials);

        await AssertProvisionerIsBoundedAsync(iamContext);
        foreach (var localProfile in LocalServiceIdentityProfileCatalog.All)
        {
            await AssertExactIamProfileAsync(iamContext, localProfile);
        }

        await using var authContext = CreateAuthContext();
        await authContext.Database.OpenConnectionAsync();
        await authContext.Database.CloseConnectionAsync();
        await RunAuthSeederAsync(authContext, iamContext, firstMaterials);
        await AssertCurrentCredentialCatalogAsync(authContext, firstMaterials, expectedVersionsPerCredential: 1);

        await RunIamSeederAsync(iamContext, firstMaterials);
        await RunAuthSeederAsync(authContext, iamContext, firstMaterials);
        Assert.Equal(
            LocalServiceIdentityProfileCatalog.All.Count,
            await authContext.ServiceCredentialVersions.CountAsync());

        var secondMaterials = LocalServiceIdentitySeedMaterial.CreateCatalog();
        await RunAuthSeederAsync(authContext, iamContext, secondMaterials);
        await AssertCurrentCredentialCatalogAsync(authContext, secondMaterials, expectedVersionsPerCredential: 2);

        var thirdMaterials = LocalServiceIdentitySeedMaterial.CreateCatalog();
        await using (var failingAuthContext = CreateAuthContext(new ThrowOnSaveChangesInterceptor(2)))
        {
            await Assert.ThrowsAsync<InjectedSeedFailureException>(() =>
                RunAuthSeederAsync(failingAuthContext, iamContext, thirdMaterials));
        }

        await using (var verificationContext = CreateAuthContext())
        {
            await AssertCurrentCredentialCatalogAsync(
                verificationContext,
                secondMaterials,
                expectedVersionsPerCredential: 2);
        }

        var contactProfile = LocalServiceIdentityProfileCatalog.ContactService;
        var contactPrincipal = await iamContext.Principals.SingleAsync(
            principal => principal.WorkloadId == contactProfile.WorkloadId);
        var scopedBinding = await iamContext.PrincipalRoleBindings.SingleAsync(
            binding => binding.PrincipalId == contactPrincipal.PrincipalId &&
                binding.ResourcePath == "folders/contacts");
        scopedBinding.ResourcePath = "folders/archive";
        await iamContext.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RunAuthSeederAsync(authContext, iamContext, secondMaterials));

        iamContext.ChangeTracker.Clear();
        scopedBinding = await iamContext.PrincipalRoleBindings.SingleAsync(
            binding => binding.PrincipalId == contactPrincipal.PrincipalId &&
                binding.ResourcePath == "folders/archive");
        scopedBinding.ResourcePath = "folders/contacts";
        iamContext.PrincipalPermissionBindings.Add(new PrincipalPermissionBinding
        {
            BindingId = Guid.NewGuid(),
            PrincipalId = contactPrincipal.PrincipalId,
            PermissionId = "country.countries.read",
            ResourcePath = null,
            GrantedAt = DateTime.UtcNow
        });
        await iamContext.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            RunAuthSeederAsync(authContext, iamContext, secondMaterials));

        iamContext.ChangeTracker.Clear();
        var directBinding = await iamContext.PrincipalPermissionBindings.SingleAsync(
            binding => binding.PrincipalId == contactPrincipal.PrincipalId);
        iamContext.PrincipalPermissionBindings.Remove(directBinding);
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
            RunIamSeederAsync(iamContext, secondMaterials));
    }

    private static async Task AssertProvisionerIsBoundedAsync(IAMDbContext context)
    {
        var actor = await context.Principals.AsNoTracking().SingleAsync(
            principal => principal.PrincipalId == LocalServiceIdentitySeedOptions.ProvisionerPrincipalId);
        Assert.Equal("user", actor.PrincipalType);
        Assert.True(actor.IsActive);
        Assert.Null(actor.WorkloadId);

        var actorBinding = await context.PrincipalRoleBindings.AsNoTracking().SingleAsync(
            binding => binding.PrincipalId == actor.PrincipalId);
        Assert.Equal(LocalServiceIdentitySeedOptions.ProvisionerRoleId, actorBinding.RoleId);
        Assert.Null(actorBinding.ResourcePath);
        Assert.Null(actorBinding.ExpiresAt);

        var actorPermissions = await (
            from binding in context.PrincipalRoleBindings
            join rolePermission in context.RolePermissions on binding.RoleId equals rolePermission.RoleId
            where binding.PrincipalId == actor.PrincipalId
            select rolePermission.PermissionId).ToListAsync();
        Assert.Equal([LocalServiceIdentitySeedOptions.ProvisionPermission], actorPermissions);
        Assert.DoesNotContain("*", actorPermissions);
        Assert.DoesNotContain(
            await context.PrincipalRoleBindings.AsNoTracking()
                .Where(binding => binding.PrincipalId == actor.PrincipalId)
                .Select(binding => binding.RoleId)
                .ToListAsync(),
            roleId => roleId.Contains("platform.owner", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task AssertExactIamProfileAsync(
        IAMDbContext context,
        LocalServiceIdentityProfile localProfile)
    {
        var expected = WorkloadAccessProfileCatalog.Default.Get(
            localProfile.WorkloadId,
            localProfile.ProfileVersion);
        var principal = await context.Principals.AsNoTracking().SingleAsync(
            candidate => candidate.WorkloadId == localProfile.WorkloadId);
        var bindings = await context.PrincipalRoleBindings.AsNoTracking()
            .Where(binding => binding.PrincipalId == principal.PrincipalId)
            .OrderBy(binding => binding.ResourcePath)
            .ToListAsync();
        Assert.Equal(1 + expected.AdditionalGrants.Count, bindings.Count);
        Assert.Contains(bindings, binding =>
            binding.RoleId == expected.RoleId && binding.ResourcePath is null);
        foreach (var grant in expected.AdditionalGrants)
        {
            Assert.Contains(bindings, binding =>
                binding.RoleId == grant.RoleId && binding.ResourcePath == grant.ResourcePath);
        }

        Assert.Empty(await context.PrincipalPermissionBindings.AsNoTracking()
            .Where(binding => binding.PrincipalId == principal.PrincipalId)
            .ToListAsync());
        Assert.Empty(await context.ServiceAccountApiKeys.AsNoTracking()
            .Where(key => key.PrincipalId == principal.PrincipalId)
            .ToListAsync());
    }

    private static async Task AssertCurrentCredentialCatalogAsync(
        AuthDbContext context,
        IReadOnlyDictionary<string, LocalServiceIdentitySeedMaterial> materials,
        int expectedVersionsPerCredential)
    {
        var credentials = await context.ServiceCredentials.AsNoTracking()
            .Include(credential => credential.Versions)
            .OrderBy(credential => credential.WorkloadId)
            .ToListAsync();
        Assert.Equal(LocalServiceIdentityProfileCatalog.All.Count, credentials.Count);
        foreach (var profile in LocalServiceIdentityProfileCatalog.All)
        {
            var credential = Assert.Single(credentials, candidate =>
                candidate.WorkloadId == profile.WorkloadId);
            var material = materials[profile.WorkloadId];
            Assert.Equal(profile.ClientId, credential.ClientId);
            Assert.Equal(profile.ServiceName, credential.ServiceName);
            Assert.Equal(profile.RoleId, credential.RoleId);
            Assert.Equal(material.SecretHash, credential.ClientSecretHash);
            Assert.Equal(expectedVersionsPerCredential, credential.Versions.Count);
            Assert.Equal(
                material.SecretHash,
                Assert.Single(
                    credential.Versions,
                    version => version.Status == ServiceCredentialVersionStatus.Active).SecretHash);
            Assert.DoesNotContain(material.RawSecret, GetPersistedCredentialStrings(credential));
        }
    }

    private static PostgreSqlContainer CreatePostgres(string database) =>
        new PostgreSqlBuilder("postgres:18-alpine")
            .WithDatabase(database)
            .WithUsername("test_user")
            .WithPassword("test_password")
            .WithPortBinding(5432, true)
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

    private static async Task RunIamSeederAsync(
        IAMDbContext context,
        IReadOnlyDictionary<string, LocalServiceIdentitySeedMaterial> materials)
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
            BuildConfiguration(materials),
            BuildEnvironment(),
            provisioner);
        await seeder.ExecuteAsync();
    }

    private static async Task RunAuthSeederAsync(
        AuthDbContext authContext,
        IAMDbContext iamContext,
        IReadOnlyDictionary<string, LocalServiceIdentitySeedMaterial> materials)
    {
        var seeder = new AuthServiceIdentityDatabaseSeeder(
            authContext,
            iamContext,
            NullLogger<AuthServiceIdentityDatabaseSeeder>.Instance,
            BuildConfiguration(materials),
            BuildEnvironment());
        await seeder.ExecuteAsync();
    }

    private static IConfiguration BuildConfiguration(
        IReadOnlyDictionary<string, LocalServiceIdentitySeedMaterial> materials)
    {
        var values = new Dictionary<string, string?>
        {
            ["AspireLocalServiceIdentity:Enabled"] = "true",
            ["AspireTestAdmin:Enabled"] = "false"
        };
        foreach (var profile in LocalServiceIdentityProfileCatalog.All)
        {
            values[$"AspireLocalServiceIdentity:Profiles:{profile.WorkloadId}:SecretHash"] =
                materials[profile.WorkloadId].SecretHash;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

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
