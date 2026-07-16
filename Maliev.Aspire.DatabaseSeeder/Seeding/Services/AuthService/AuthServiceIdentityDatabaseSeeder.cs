using System.Data;
using global::Maliev.Aspire.DatabaseSeeder.Seeding.Core;
using global::Maliev.Aspire.DatabaseSeeder.Seeding.Services.Shared;
using global::Maliev.AuthService.Domain.Entities;
using global::Maliev.AuthService.Infrastructure.DbContexts;
using global::Maliev.IAMService.Domain.Entities;
using global::Maliev.IAMService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Maliev.Aspire.DatabaseSeeder.Seeding.Services.AuthService;

/// <summary>
/// Persists the verifier-only AuthService credential after validating its exact IAM workload state.
/// </summary>
public sealed class AuthServiceIdentityDatabaseSeeder : DatabaseSeeder<AuthDbContext>
{
    private static readonly TimeSpan CredentialLifetime = TimeSpan.FromDays(7);
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IAMDbContext _iamDbContext;

    /// <summary>Initializes the local AuthService identity seeder.</summary>
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
            Logger.LogInformation("Aspire-local AuthService identity seeding is disabled.");
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
            await SeedCredentialAsync(options, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        });
    }

    private async Task SeedCredentialAsync(
        LocalServiceIdentitySeedOptions options,
        CancellationToken cancellationToken)
    {
        var workloadPrincipal = await GetExactIamWorkloadPrincipalAsync(cancellationToken);
        var credential = await GetCredentialAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;

        if (credential is null)
        {
            credential = new ServiceCredential
            {
                Id = Guid.NewGuid(),
                ClientId = LocalServiceIdentitySeedOptions.ClientId,
                PrincipalId = workloadPrincipal.PrincipalId,
                WorkloadId = LocalServiceIdentitySeedOptions.WorkloadId,
                ProfileVersion = LocalServiceIdentitySeedOptions.ProfileVersion,
                RoleId = LocalServiceIdentitySeedOptions.RoleId,
                ClientSecretHash = options.SecretHash,
                ServiceName = LocalServiceIdentitySeedOptions.ServiceName,
                IsActive = true,
                CreatedAt = now.UtcDateTime,
                UpdatedAt = now.UtcDateTime,
                RevokedAt = null
            };
            Context.ServiceCredentials.Add(credential);
        }
        else
        {
            EnsureExactCredentialBinding(credential, workloadPrincipal.PrincipalId);
        }

        var currentActive = credential.Versions.SingleOrDefault(
            version => version.Status == ServiceCredentialVersionStatus.Active);
        if (currentActive is not null &&
            string.Equals(currentActive.SecretHash, options.SecretHash, StringComparison.Ordinal) &&
            currentActive.CanAuthenticate(now))
        {
            credential.ClientSecretHash = options.SecretHash;
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

        credential.ClientSecretHash = options.SecretHash;
        credential.IsActive = true;
        credential.RevokedAt = null;
        credential.UpdatedAt = now.UtcDateTime;

        // Persist revocation before inserting the new Active row so PostgreSQL's partial
        // unique index never observes two active versions during a local restart.
        await Context.SaveChangesAsync(cancellationToken);

        var nextVersion = credential.Versions.Count == 0
            ? 1
            : credential.Versions.Max(version => version.Version) + 1;
        Context.ServiceCredentialVersions.Add(new ServiceCredentialVersion
        {
            Id = Guid.NewGuid(),
            ServiceCredentialId = credential.Id,
            Version = nextVersion,
            SecretHash = options.SecretHash,
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
            LocalServiceIdentitySeedOptions.WorkloadId,
            LocalServiceIdentitySeedOptions.ProfileVersion);
    }

    private async Task<Principal> GetExactIamWorkloadPrincipalAsync(CancellationToken cancellationToken)
    {
        var principal = await _iamDbContext.Principals
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.WorkloadId == LocalServiceIdentitySeedOptions.WorkloadId,
                cancellationToken);
        if (principal is null ||
            !principal.IsActive ||
            !string.Equals(principal.PrincipalType, "service_account", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The canonical IAM auth-service workload principal is missing or inactive.");
        }

        var role = await _iamDbContext.Roles
            .AsNoTracking()
            .Include(candidate => candidate.RolePermissions)
            .SingleOrDefaultAsync(
                candidate => candidate.RoleId == LocalServiceIdentitySeedOptions.RoleId,
                cancellationToken);
        if (role is null ||
            role.IsCustom ||
            !string.Equals(role.ServiceName, "iam", StringComparison.Ordinal) ||
            role.RolePermissions.Count != 1 ||
            !string.Equals(
                role.RolePermissions.Single().PermissionId,
                LocalServiceIdentitySeedOptions.WorkloadPermission,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The canonical IAM auth-service role is missing or has authority drift.");
        }

        var bindings = await _iamDbContext.PrincipalRoleBindings
            .AsNoTracking()
            .Where(binding => binding.PrincipalId == principal.PrincipalId)
            .ToListAsync(cancellationToken);
        if (bindings.Count != 1 ||
            !string.Equals(bindings[0].RoleId, LocalServiceIdentitySeedOptions.RoleId, StringComparison.Ordinal) ||
            bindings[0].ResourcePath is not null ||
            bindings[0].ExpiresAt is not null ||
            await _iamDbContext.PrincipalPermissionBindings.AsNoTracking().AnyAsync(
                binding => binding.PrincipalId == principal.PrincipalId,
                cancellationToken) ||
            await _iamDbContext.ServiceAccountApiKeys.AsNoTracking().AnyAsync(
                key => key.PrincipalId == principal.PrincipalId,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "The canonical IAM auth-service principal has authority outside its exact profile.");
        }

        return principal;
    }

    private async Task<ServiceCredential?> GetCredentialAsync(CancellationToken cancellationToken)
    {
        var byWorkload = await Context.ServiceCredentials
            .Include(credential => credential.Versions)
            .SingleOrDefaultAsync(
                credential => credential.WorkloadId == LocalServiceIdentitySeedOptions.WorkloadId,
                cancellationToken);
        var byClient = await Context.ServiceCredentials
            .Include(credential => credential.Versions)
            .SingleOrDefaultAsync(
                credential => credential.ClientId == LocalServiceIdentitySeedOptions.ClientId,
                cancellationToken);
        if (byWorkload is not null && byClient is not null && byWorkload.Id != byClient.Id)
        {
            throw new InvalidOperationException(
                "The Aspire-local AuthService workload and client identifiers belong to different credentials.");
        }

        return byWorkload ?? byClient;
    }

    private static void EnsureExactCredentialBinding(ServiceCredential credential, Guid principalId)
    {
        if (!string.Equals(credential.ClientId, LocalServiceIdentitySeedOptions.ClientId, StringComparison.Ordinal) ||
            credential.PrincipalId != principalId ||
            !string.Equals(credential.WorkloadId, LocalServiceIdentitySeedOptions.WorkloadId, StringComparison.Ordinal) ||
            credential.ProfileVersion != LocalServiceIdentitySeedOptions.ProfileVersion ||
            !string.Equals(credential.RoleId, LocalServiceIdentitySeedOptions.RoleId, StringComparison.Ordinal) ||
            !string.Equals(credential.ServiceName, LocalServiceIdentitySeedOptions.ServiceName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The Aspire-local AuthService credential has identity or IAM binding drift.");
        }
    }
}
