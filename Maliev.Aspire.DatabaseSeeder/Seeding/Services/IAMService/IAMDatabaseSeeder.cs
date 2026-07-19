using Maliev.Aspire.DatabaseSeeder.Seeding.Core;
using Maliev.Aspire.DatabaseSeeder.Seeding.Services.Shared;
using Maliev.IAMService.Application.DTOs.Requests;
using Maliev.IAMService.Application.Workloads;
using Maliev.IAMService.Domain.Entities;
using Maliev.IAMService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Maliev.Aspire.DatabaseSeeder.Seeding.Services.IAMService;

/// <summary>
/// Database seeder for Aspire-local IAM test administrator access.
/// </summary>
public class IAMDatabaseSeeder : DatabaseSeeder<IAMDbContext>
{
    private const string PlatformOwnerRoleId = "roles.platform.owner";
    private const string AutomationRoleName = "Aspire Automation";
    private const string LimitedRoleName = "Aspire Limited Employee";
    private const string LocalProvisionerEmail = "aspire-workload-provisioner@local.maliev.invalid";
    private const string LocalProvisionerRoleName = "Aspire Workload Provisioner";
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IWorkloadPrincipalProvisioner _workloadPrincipalProvisioner;

    /// <summary>
    /// Initializes a new instance of the <see cref="IAMDatabaseSeeder"/> class.
    /// </summary>
    /// <param name="context">The IAM database context.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <param name="hostEnvironment">Current host environment.</param>
    /// <param name="workloadPrincipalProvisioner">Canonical workload provisioner.</param>
    public IAMDatabaseSeeder(
        IAMDbContext context,
        ILogger<IAMDatabaseSeeder> logger,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        IWorkloadPrincipalProvisioner workloadPrincipalProvisioner)
        : base(context, logger)
    {
        _configuration = configuration;
        _hostEnvironment = hostEnvironment;
        _workloadPrincipalProvisioner = workloadPrincipalProvisioner;
    }

    /// <inheritdoc />
    protected override async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var localIdentityEnabled = LocalServiceIdentitySeedOptions.IsEnabled(
            _configuration,
            _hostEnvironment.EnvironmentName);
        if (localIdentityEnabled)
        {
            await EnsureLocalWorkloadProvisionerAsync(cancellationToken);
            foreach (var profile in LocalServiceIdentityProfileCatalog.All)
            {
                await _workloadPrincipalProvisioner.ProvisionAsync(
                    profile.WorkloadId,
                    new ProvisionWorkloadPrincipalRequest
                    {
                        ProfileVersion = profile.ProfileVersion,
                        OperationId = profile.ProvisionOperationId
                    },
                    LocalServiceIdentitySeedOptions.ProvisionerPrincipalId,
                    cancellationToken);

                Logger.LogInformation(
                    "Provisioned exact Aspire-local IAM workload profile {WorkloadId} v{ProfileVersion}.",
                    profile.WorkloadId,
                    profile.ProfileVersion);
            }
        }

        var options = AspireTestAdminSeedOptions.FromConfiguration(_configuration);
        if (!options.Enabled)
        {
            Logger.LogInformation("Aspire local test administrator seeding is disabled.");
            return;
        }

        await EnsurePrincipalAsync(options, cancellationToken);
        await EnsureAutomationRoleAsync(options, cancellationToken);
        await RemoveLegacyPlatformOwnerBindingAsync(options, cancellationToken);
        await EnsureAutomationRoleBindingAsync(options, cancellationToken);
        await EnsureLimitedPrincipalAsync(options, cancellationToken);
        await EnsureLimitedRoleAsync(options, cancellationToken);
        await RemoveUnexpectedLimitedBindingsAsync(options, cancellationToken);
        await EnsureLimitedRoleBindingAsync(options, cancellationToken);

        Logger.LogInformation(
            "Successfully seeded Aspire local IAM browser principals {AdminEmail} and {LimitedEmail}.",
            options.Email,
            options.LimitedEmail);
    }

    private async Task EnsureLocalWorkloadProvisionerAsync(CancellationToken cancellationToken)
    {
        await EnsureLocalPermissionAsync(
            LocalServiceIdentitySeedOptions.ProvisionPermission,
            "Allows the Aspire-local bootstrap actor to provision declared workload profiles.",
            cancellationToken);
        foreach (var localProfile in LocalServiceIdentityProfileCatalog.All)
        {
            var profile = WorkloadAccessProfileCatalog.Default.Get(
                localProfile.WorkloadId,
                localProfile.ProfileVersion);
            var permissionIds = profile.Permissions
                .Concat(profile.AdditionalGrants.SelectMany(grant => grant.Permissions))
                .Distinct(StringComparer.Ordinal);
            foreach (var permissionId in permissionIds)
            {
                await EnsureLocalPermissionAsync(
                    permissionId,
                    $"Aspire-local workload permission for {localProfile.WorkloadId}.",
                    cancellationToken);
            }
        }

        var conflictingEmail = await Context.Principals
            .AsNoTracking()
            .SingleOrDefaultAsync(
                principal => principal.Email == LocalProvisionerEmail &&
                    principal.PrincipalId != LocalServiceIdentitySeedOptions.ProvisionerPrincipalId,
                cancellationToken);
        if (conflictingEmail is not null)
        {
            throw new InvalidOperationException(
                "The Aspire-local workload provisioner email belongs to another principal.");
        }

        var actor = await Context.Principals.SingleOrDefaultAsync(
            principal => principal.PrincipalId == LocalServiceIdentitySeedOptions.ProvisionerPrincipalId,
            cancellationToken);
        if (actor is not null &&
            (!string.Equals(actor.PrincipalType, "user", StringComparison.Ordinal) ||
             actor.WorkloadId is not null ||
             !string.Equals(actor.Email, LocalProvisionerEmail, StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "The Aspire-local workload provisioner principal has incompatible identity state.");
        }

        var role = await Context.Roles
            .Include(candidate => candidate.RolePermissions)
            .SingleOrDefaultAsync(
                candidate => candidate.RoleId == LocalServiceIdentitySeedOptions.ProvisionerRoleId,
                cancellationToken);
        if (role is not null &&
            (!role.IsCustom ||
             !string.Equals(role.ServiceName, "aspire", StringComparison.Ordinal) ||
             role.RolePermissions.Count != 1 ||
             !string.Equals(
                 role.RolePermissions.Single().PermissionId,
                 LocalServiceIdentitySeedOptions.ProvisionPermission,
                 StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "The Aspire-local workload provisioner role has authority outside its exact contract.");
        }

        if (await Context.PrincipalPermissionBindings.AnyAsync(
                binding => binding.PrincipalId == LocalServiceIdentitySeedOptions.ProvisionerPrincipalId,
                cancellationToken) ||
            await Context.ServiceAccountApiKeys.AnyAsync(
                key => key.PrincipalId == LocalServiceIdentitySeedOptions.ProvisionerPrincipalId,
                cancellationToken))
        {
            throw new InvalidOperationException(
                "The Aspire-local workload provisioner has unexpected direct authority or API keys.");
        }

        var bindings = await Context.PrincipalRoleBindings
            .Where(binding => binding.PrincipalId == LocalServiceIdentitySeedOptions.ProvisionerPrincipalId)
            .ToListAsync(cancellationToken);
        if (bindings.Count > 0 &&
            (bindings.Count != 1 ||
             !string.Equals(
                 bindings[0].RoleId,
                 LocalServiceIdentitySeedOptions.ProvisionerRoleId,
                 StringComparison.Ordinal) ||
             bindings[0].ResourcePath is not null ||
             bindings[0].ExpiresAt is not null))
        {
            throw new InvalidOperationException(
                "The Aspire-local workload provisioner binding has authority outside its exact contract.");
        }

        var now = DateTime.UtcNow;
        if (actor is null)
        {
            actor = new Principal
            {
                PrincipalId = LocalServiceIdentitySeedOptions.ProvisionerPrincipalId,
                PrincipalType = "user",
                Email = LocalProvisionerEmail,
                DisplayName = LocalProvisionerRoleName,
                IsActive = true,
                LinkedService = "AspireLocalIdentitySeeder",
                CreatedAt = now,
                UpdatedAt = now
            };
            Context.Principals.Add(actor);
        }
        else
        {
            actor.DisplayName = LocalProvisionerRoleName;
            actor.IsActive = true;
            actor.LinkedService = "AspireLocalIdentitySeeder";
            actor.UpdatedAt = now;
        }

        if (role is null)
        {
            role = new Role
            {
                RoleId = LocalServiceIdentitySeedOptions.ProvisionerRoleId,
                RoleName = LocalProvisionerRoleName,
                ServiceName = "aspire",
                Description = "Local-only role with the single workload provisioning permission.",
                IsCustom = true,
                CreatedBy = LocalServiceIdentitySeedOptions.ProvisionerPrincipalId,
                CreatedAt = now,
                UpdatedAt = now,
                RolePermissions =
                [
                    new RolePermission
                    {
                        RoleId = LocalServiceIdentitySeedOptions.ProvisionerRoleId,
                        PermissionId = LocalServiceIdentitySeedOptions.ProvisionPermission,
                        AddedAt = now
                    }
                ]
            };
            Context.Roles.Add(role);
        }

        if (bindings.Count == 0)
        {
            Context.PrincipalRoleBindings.Add(new PrincipalRoleBinding
            {
                BindingId = Guid.NewGuid(),
                PrincipalId = LocalServiceIdentitySeedOptions.ProvisionerPrincipalId,
                RoleId = LocalServiceIdentitySeedOptions.ProvisionerRoleId,
                ResourcePath = null,
                GrantedBy = LocalServiceIdentitySeedOptions.ProvisionerPrincipalId,
                GrantedAt = now,
                ExpiresAt = null
            });
        }

        await Context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureLocalPermissionAsync(
        string permissionId,
        string description,
        CancellationToken cancellationToken)
    {
        if (await Context.Permissions.AnyAsync(
                permission => permission.PermissionId == permissionId,
                cancellationToken))
        {
            return;
        }

        var parts = permissionId.Split('.');
        Context.Permissions.Add(new Permission
        {
            PermissionId = permissionId,
            ServiceName = parts[0],
            ResourceType = parts[1],
            Action = parts[2],
            Description = description,
            RegisteredAt = DateTime.UtcNow
        });
        await Context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsurePrincipalAsync(
        AspireTestAdminSeedOptions options,
        CancellationToken cancellationToken)
    {
        var conflictingEmailPrincipal = await Context.Principals
            .FirstOrDefaultAsync(p => p.Email == options.Email && p.PrincipalId != options.PrincipalId, cancellationToken);

        if (conflictingEmailPrincipal != null)
        {
            throw new InvalidOperationException(
                $"Cannot seed Aspire test admin because {options.Email} is already assigned to principal " +
                $"{conflictingEmailPrincipal.PrincipalId}.");
        }

        var principal = await Context.Principals
            .FirstOrDefaultAsync(p => p.PrincipalId == options.PrincipalId, cancellationToken);

        var isNew = principal == null;
        principal ??= new Principal
        {
            PrincipalId = options.PrincipalId,
            CreatedAt = DateTime.UtcNow
        };

        principal.PrincipalType = "user";
        principal.Email = options.Email;
        principal.DisplayName = options.PreferredName;
        principal.IsActive = true;
        principal.LinkedService = options.LinkedService;
        principal.LinkedEntityId = options.EmployeeId;
        principal.UpdatedAt = DateTime.UtcNow;

        if (isNew)
        {
            Context.Principals.Add(principal);
        }

        await Context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureAutomationRoleAsync(
        AspireTestAdminSeedOptions options,
        CancellationToken cancellationToken)
    {
        var wildcardPermission = await Context.Permissions
            .FirstOrDefaultAsync(p => p.PermissionId == "*", cancellationToken);

        if (wildcardPermission == null)
        {
            Context.Permissions.Add(new Permission
            {
                PermissionId = "*",
                ServiceName = "platform",
                ResourceType = "all",
                Action = "all",
                Description = "Wildcard permission for full access",
                RegisteredAt = DateTime.UtcNow
            });
        }

        var role = await Context.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.RoleId == options.RoleId, cancellationToken);

        if (role == null)
        {
            role = new Role
            {
                RoleId = options.RoleId,
                RoleName = AutomationRoleName,
                ServiceName = "aspire",
                Description = "Aspire-local automation role with wildcard access for browser and system validation",
                IsCustom = true,
                CreatedBy = IAMDbContext.SystemPrincipalId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            role.RolePermissions.Add(new RolePermission
            {
                RoleId = options.RoleId,
                PermissionId = "*"
            });

            Context.Roles.Add(role);
        }
        else
        {
            role.RoleName = AutomationRoleName;
            role.ServiceName = "aspire";
            role.Description = "Aspire-local automation role with wildcard access for browser and system validation";
            role.IsCustom = true;
            role.UpdatedAt = DateTime.UtcNow;

            if (!role.RolePermissions.Any(rp => rp.PermissionId == "*"))
            {
                role.RolePermissions.Add(new RolePermission
                {
                    RoleId = options.RoleId,
                    PermissionId = "*"
                });
            }
        }

        await Context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureLimitedPrincipalAsync(
        AspireTestAdminSeedOptions options,
        CancellationToken cancellationToken)
    {
        var conflictingEmailPrincipal = await Context.Principals
            .FirstOrDefaultAsync(p => p.Email == options.LimitedEmail && p.PrincipalId != options.LimitedPrincipalId, cancellationToken);

        if (conflictingEmailPrincipal != null)
        {
            throw new InvalidOperationException(
                $"Cannot seed Aspire limited employee because {options.LimitedEmail} is already assigned to principal " +
                $"{conflictingEmailPrincipal.PrincipalId}.");
        }

        var principal = await Context.Principals
            .FirstOrDefaultAsync(p => p.PrincipalId == options.LimitedPrincipalId, cancellationToken);

        var isNew = principal == null;
        principal ??= new Principal
        {
            PrincipalId = options.LimitedPrincipalId,
            CreatedAt = DateTime.UtcNow
        };

        principal.PrincipalType = "user";
        principal.Email = options.LimitedEmail;
        principal.DisplayName = options.LimitedPreferredName;
        principal.IsActive = true;
        principal.LinkedService = options.LinkedService;
        principal.LinkedEntityId = options.LimitedEmployeeId;
        principal.UpdatedAt = DateTime.UtcNow;

        if (isNew)
        {
            Context.Principals.Add(principal);
        }

        await Context.SaveChangesAsync(cancellationToken);
    }

    private async Task EnsureLimitedRoleAsync(
        AspireTestAdminSeedOptions options,
        CancellationToken cancellationToken)
    {
        var permissionIds = options.LimitedRolePermissions.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        foreach (var permissionId in permissionIds)
        {
            var permission = await Context.Permissions
                .FirstOrDefaultAsync(p => p.PermissionId == permissionId, cancellationToken);

            if (permission == null)
            {
                var parts = permissionId.Split('.');
                Context.Permissions.Add(new Permission
                {
                    PermissionId = permissionId,
                    ServiceName = parts[0],
                    ResourceType = parts[1],
                    Action = parts[2],
                    Description = $"Aspire limited employee permission {permissionId}",
                    RegisteredAt = DateTime.UtcNow
                });
            }
        }

        var role = await Context.Roles
            .Include(r => r.RolePermissions)
            .FirstOrDefaultAsync(r => r.RoleId == options.LimitedRoleIdValue, cancellationToken);

        if (role == null)
        {
            role = new Role
            {
                RoleId = options.LimitedRoleIdValue,
                RoleName = LimitedRoleName,
                ServiceName = "aspire",
                Description = "Aspire-local limited employee role for browser permission-boundary validation",
                IsCustom = true,
                CreatedBy = IAMDbContext.SystemPrincipalId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            Context.Roles.Add(role);
        }
        else
        {
            role.RoleName = LimitedRoleName;
            role.ServiceName = "aspire";
            role.Description = "Aspire-local limited employee role for browser permission-boundary validation";
            role.IsCustom = true;
            role.UpdatedAt = DateTime.UtcNow;
        }

        var stalePermissions = role.RolePermissions
            .Where(rolePermission => !permissionIds.Contains(rolePermission.PermissionId, StringComparer.OrdinalIgnoreCase))
            .ToList();
        Context.RolePermissions.RemoveRange(stalePermissions);

        foreach (var permissionId in permissionIds)
        {
            if (!role.RolePermissions.Any(rolePermission => string.Equals(rolePermission.PermissionId, permissionId, StringComparison.OrdinalIgnoreCase)))
            {
                role.RolePermissions.Add(new RolePermission
                {
                    RoleId = options.LimitedRoleIdValue,
                    PermissionId = permissionId
                });
            }
        }

        await Context.SaveChangesAsync(cancellationToken);
    }

    private async Task RemoveLegacyPlatformOwnerBindingAsync(
        AspireTestAdminSeedOptions options,
        CancellationToken cancellationToken)
    {
        var legacyBindings = await Context.PrincipalRoleBindings
            .Where(b => b.PrincipalId == options.PrincipalId && b.RoleId == PlatformOwnerRoleId)
            .ToListAsync(cancellationToken);

        if (legacyBindings.Count == 0)
        {
            return;
        }

        Context.PrincipalRoleBindings.RemoveRange(legacyBindings);
        await Context.SaveChangesAsync(cancellationToken);

        Logger.LogInformation(
            "Removed {Count} legacy Platform Owner binding(s) from Aspire automation principal {PrincipalId}.",
            legacyBindings.Count,
            options.PrincipalId);
    }

    private async Task EnsureAutomationRoleBindingAsync(
        AspireTestAdminSeedOptions options,
        CancellationToken cancellationToken)
    {
        var bindingExists = await Context.PrincipalRoleBindings.AnyAsync(
            b => b.PrincipalId == options.PrincipalId &&
                 b.RoleId == options.RoleId &&
                 b.ResourcePath == "*",
            cancellationToken);

        if (bindingExists)
        {
            return;
        }

        Context.PrincipalRoleBindings.Add(new PrincipalRoleBinding
        {
            BindingId = Guid.NewGuid(),
            PrincipalId = options.PrincipalId,
            RoleId = options.RoleId,
            ResourcePath = "*",
            GrantedBy = IAMDbContext.SystemPrincipalId,
            GrantedAt = DateTime.UtcNow
        });

        await Context.SaveChangesAsync(cancellationToken);
    }

    private async Task RemoveUnexpectedLimitedBindingsAsync(
        AspireTestAdminSeedOptions options,
        CancellationToken cancellationToken)
    {
        var unexpectedBindings = await Context.PrincipalRoleBindings
            .Where(b => b.PrincipalId == options.LimitedPrincipalId && b.RoleId != options.LimitedRoleIdValue)
            .ToListAsync(cancellationToken);

        if (unexpectedBindings.Count == 0)
        {
            return;
        }

        Context.PrincipalRoleBindings.RemoveRange(unexpectedBindings);
        await Context.SaveChangesAsync(cancellationToken);

        Logger.LogInformation(
            "Removed {Count} unexpected role binding(s) from Aspire limited employee principal {PrincipalId}.",
            unexpectedBindings.Count,
            options.LimitedPrincipalId);
    }

    private async Task EnsureLimitedRoleBindingAsync(
        AspireTestAdminSeedOptions options,
        CancellationToken cancellationToken)
    {
        var bindingExists = await Context.PrincipalRoleBindings.AnyAsync(
            b => b.PrincipalId == options.LimitedPrincipalId &&
                 b.RoleId == options.LimitedRoleIdValue &&
                 b.ResourcePath == "*",
            cancellationToken);

        if (bindingExists)
        {
            return;
        }

        Context.PrincipalRoleBindings.Add(new PrincipalRoleBinding
        {
            BindingId = Guid.NewGuid(),
            PrincipalId = options.LimitedPrincipalId,
            RoleId = options.LimitedRoleIdValue,
            ResourcePath = "*",
            GrantedBy = IAMDbContext.SystemPrincipalId,
            GrantedAt = DateTime.UtcNow
        });

        await Context.SaveChangesAsync(cancellationToken);
    }
}
