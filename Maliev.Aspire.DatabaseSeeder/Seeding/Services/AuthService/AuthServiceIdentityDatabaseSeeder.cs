using System.Data;
using global::Maliev.Aspire.DatabaseSeeder.Seeding.Core;
using global::Maliev.Aspire.DatabaseSeeder.Seeding.Services.Shared;
using global::Maliev.AuthService.Domain.Entities;
using global::Maliev.AuthService.Infrastructure.DbContexts;
using global::Maliev.IAMService.Application.Workloads;
using global::Maliev.IAMService.Domain.Entities;
using global::Maliev.IAMService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Maliev.Aspire.DatabaseSeeder.Seeding.Services.AuthService;

/// <summary>
/// Persists verifier-only local credentials after validating each exact IAM workload profile.
/// </summary>
public sealed class AuthServiceIdentityDatabaseSeeder : DatabaseSeeder<AuthDbContext>
{
    private static readonly TimeSpan CredentialLifetime = TimeSpan.FromDays(7);
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IAMDbContext _iamDbContext;

    /// <summary>Initializes the local workload identity seeder.</summary>
    /// <param name="context">Auth database context.</param>
    /// <param name="iamDbContext">IAM database context used only for exact-state validation.</param>
    /// <param name="logger">Seeder logger.</param>
    /// <param name="configuration">Seeder configuration.</param>
    /// <param name="hostEnvironment">Current host environment.</param>
    public AuthServiceIdentityDatabaseSeeder(
        AuthDbContext context,
        IAMDbContext iamDbContext,
        ILogger<AuthServiceIdentityDatabaseSeeder> logger,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment)
        : base(context, logger)
    {
        _iamDbContext = iamDbContext;
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
    }

    /// <inheritdoc />
    protected override async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var options = LocalServiceIdentitySeedOptions.FromConfiguration(
            _configuration,
            _hostEnvironment.EnvironmentName);
        if (!options.Enabled)
        {
            Logger.LogInformation("Aspire-local workload identity seeding is disabled.");
            return;
        }

        var strategy = Context.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            Context.ChangeTracker.Clear();
            await using var transaction = await Context.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
            await Context.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_xact_lock(2071220626)",
                cancellationToken);
            foreach (var profile in LocalServiceIdentityProfileCatalog.All)
            {
                await SeedCredentialAsync(
                    profile,
                    options.GetSecretHash(profile),
                    cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        });
    }

    private async Task SeedCredentialAsync(
        LocalServiceIdentityProfile profile,
        string secretHash,
        CancellationToken cancellationToken)
    {
        var workloadPrincipal = await GetExactIamWorkloadPrincipalAsync(profile, cancellationToken);
        var credential = await GetCredentialAsync(profile, cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (credential is null)
        {
            credential = new ServiceCredential
            {
                Id = Guid.NewGuid(),
                ClientId = profile.ClientId,
                PrincipalId = workloadPrincipal.PrincipalId,
                WorkloadId = profile.WorkloadId,
                ProfileVersion = profile.ProfileVersion,
                RoleId = profile.RoleId,
                ClientSecretHash = secretHash,
                ServiceName = profile.ServiceName,
                IsActive = true,
                CreatedAt = now.UtcDateTime,
                UpdatedAt = now.UtcDateTime,
                RevokedAt = null
            };
            Context.ServiceCredentials.Add(credential);
        }
        else
        {
            EnsureExactCredentialBinding(credential, profile, workloadPrincipal.PrincipalId);
        }

        var currentActive = credential.Versions.SingleOrDefault(
            version => version.Status == ServiceCredentialVersionStatus.Active);
        if (currentActive is not null &&
            string.Equals(currentActive.SecretHash, secretHash, StringComparison.Ordinal) &&
            currentActive.CanAuthenticate(now))
        {
            credential.ClientSecretHash = secretHash;
            credential.IsActive = true;
            credential.RevokedAt = null;
            credential.UpdatedAt = now.UtcDateTime;
            await Context.SaveChangesAsync(cancellationToken);
            return;
        }

        foreach (var version in credential.Versions.Where(
                     version => version.Status is ServiceCredentialVersionStatus.Pending or
                         ServiceCredentialVersionStatus.Active or
                         ServiceCredentialVersionStatus.Grace))
        {
            version.Status = ServiceCredentialVersionStatus.Revoked;
            version.RevokedAt = now;
            version.GraceExpiresAt = null;
        }

        credential.ClientSecretHash = secretHash;
        credential.IsActive = true;
        credential.RevokedAt = null;
        credential.UpdatedAt = now.UtcDateTime;
        await Context.SaveChangesAsync(cancellationToken);

        var nextVersion = credential.Versions.Count == 0
            ? 1
            : credential.Versions.Max(version => version.Version) + 1;
        Context.ServiceCredentialVersions.Add(new ServiceCredentialVersion
        {
            Id = Guid.NewGuid(),
            ServiceCredentialId = credential.Id,
            Version = nextVersion,
            SecretHash = secretHash,
            Status = ServiceCredentialVersionStatus.Active,
            CreatedAt = now,
            ActivatedAt = now,
            GraceExpiresAt = null,
            HardExpiresAt = now.Add(CredentialLifetime),
            RevokedAt = null
        });
        await Context.SaveChangesAsync(cancellationToken);

        Logger.LogInformation(
            "Seeded verifier-only Aspire-local credential for {WorkloadId} v{ProfileVersion}.",
            profile.WorkloadId,
            profile.ProfileVersion);
    }

    private async Task<Principal> GetExactIamWorkloadPrincipalAsync(
        LocalServiceIdentityProfile localProfile,
        CancellationToken cancellationToken)
    {
        var profile = WorkloadAccessProfileCatalog.Default.Get(
            localProfile.WorkloadId,
            localProfile.ProfileVersion);
        if (!string.Equals(profile.RoleId, localProfile.RoleId, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The local identity profile for '{localProfile.WorkloadId}' does not match IAM.");
        }

        var principal = await _iamDbContext.Principals
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.WorkloadId == localProfile.WorkloadId,
                cancellationToken);
        if (principal is null ||
            !principal.IsActive ||
            !string.Equals(principal.PrincipalType, "service_account", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The canonical IAM '{localProfile.WorkloadId}' workload principal is missing or inactive.");
        }

        var expectedGrants = new List<ExpectedGrant>
        {
            new(profile.RoleId, null, profile.Permissions)
        };
        expectedGrants.AddRange(profile.AdditionalGrants.Select(grant =>
            new ExpectedGrant(grant.RoleId, grant.ResourcePath, grant.Permissions)));
        var expectedRoleIds = expectedGrants.Select(grant => grant.RoleId).ToArray();
        var roles = await _iamDbContext.Roles
            .AsNoTracking()
            .Include(candidate => candidate.RolePermissions)
            .Where(candidate => expectedRoleIds.Contains(candidate.RoleId))
            .ToListAsync(cancellationToken);
        if (roles.Count != expectedGrants.Count || expectedGrants.Any(expected =>
                roles.SingleOrDefault(role => role.RoleId == expected.RoleId) is not { } role ||
                role.IsCustom ||
                !string.Equals(role.ServiceName, "iam", StringComparison.Ordinal) ||
                !role.RolePermissions.Select(permission => permission.PermissionId)
                    .Order(StringComparer.Ordinal)
                    .SequenceEqual(expected.Permissions.Order(StringComparer.Ordinal))))
        {
            throw new InvalidOperationException(
                $"The canonical IAM '{localProfile.WorkloadId}' roles are missing or have authority drift.");
        }

        var bindings = await _iamDbContext.PrincipalRoleBindings
            .AsNoTracking()
            .Where(binding => binding.PrincipalId == principal.PrincipalId)
            .ToListAsync(cancellationToken);
        if (bindings.Count != expectedGrants.Count || expectedGrants.Any(expected =>
                !bindings.Any(binding =>
                    string.Equals(binding.RoleId, expected.RoleId, StringComparison.Ordinal) &&
                    string.Equals(binding.ResourcePath, expected.ResourcePath, StringComparison.Ordinal) &&
                    binding.ExpiresAt is null)) ||
            await _iamDbContext.PrincipalPermissionBindings.AsNoTracking().AnyAsync(
                binding => binding.PrincipalId == principal.PrincipalId,
                cancellationToken) ||
            await _iamDbContext.ServiceAccountApiKeys.AsNoTracking().AnyAsync(
                key => key.PrincipalId == principal.PrincipalId,
                cancellationToken))
        {
            throw new InvalidOperationException(
                $"The canonical IAM '{localProfile.WorkloadId}' principal has authority outside its exact profile.");
        }

        return principal;
    }

    private async Task<ServiceCredential?> GetCredentialAsync(
        LocalServiceIdentityProfile profile,
        CancellationToken cancellationToken)
    {
        var byWorkload = await Context.ServiceCredentials
            .Include(credential => credential.Versions)
            .SingleOrDefaultAsync(
                credential => credential.WorkloadId == profile.WorkloadId,
                cancellationToken);
        var byClient = await Context.ServiceCredentials
            .Include(credential => credential.Versions)
            .SingleOrDefaultAsync(
                credential => credential.ClientId == profile.ClientId,
                cancellationToken);
        if (byWorkload is not null && byClient is not null && byWorkload.Id != byClient.Id)
        {
            throw new InvalidOperationException(
                $"The Aspire-local '{profile.WorkloadId}' workload and client identifiers belong to different credentials.");
        }

        return byWorkload ?? byClient;
    }

    private static void EnsureExactCredentialBinding(
        ServiceCredential credential,
        LocalServiceIdentityProfile profile,
        Guid principalId)
    {
        if (!string.Equals(credential.ClientId, profile.ClientId, StringComparison.Ordinal) ||
            credential.PrincipalId != principalId ||
            !string.Equals(credential.WorkloadId, profile.WorkloadId, StringComparison.Ordinal) ||
            credential.ProfileVersion != profile.ProfileVersion ||
            !string.Equals(credential.RoleId, profile.RoleId, StringComparison.Ordinal) ||
            !string.Equals(credential.ServiceName, profile.ServiceName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The Aspire-local '{profile.WorkloadId}' credential has identity or IAM binding drift.");
        }
    }

    private sealed record ExpectedGrant(
        string RoleId,
        string? ResourcePath,
        IReadOnlyList<string> Permissions);
}
