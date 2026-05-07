using Maliev.Aspire.DatabaseSeeder.Seeding.Core;
using Maliev.Aspire.DatabaseSeeder.Seeding.Services.Shared;
using Maliev.IAMService.Domain.Entities;
using Maliev.IAMService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Maliev.Aspire.DatabaseSeeder.Seeding.Services.IAMService;

/// <summary>
/// Database seeder for Aspire-local IAM test administrator access.
/// </summary>
public class IAMDatabaseSeeder : DatabaseSeeder<IAMDbContext>
{
    private const string PlatformOwnerRoleId = "roles.platform.owner";
    private const string AutomationRoleName = "Aspire Automation";
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="IAMDatabaseSeeder"/> class.
    /// </summary>
    /// <param name="context">The IAM database context.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="configuration">Application configuration.</param>
    public IAMDatabaseSeeder(
        IAMDbContext context,
        ILogger<IAMDatabaseSeeder> logger,
        IConfiguration configuration)
        : base(context, logger)
    {
        _configuration = configuration;
    }

    /// <inheritdoc />
    protected override async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var options = AspireTestAdminSeedOptions.FromConfiguration(_configuration);
        if (!options.Enabled)
        {
            Logger.LogInformation("Aspire local test administrator seeding is disabled. Skipping IAM seed.");
            return;
        }

        await EnsurePrincipalAsync(options, cancellationToken);
        await EnsureAutomationRoleAsync(options, cancellationToken);
        await RemoveLegacyPlatformOwnerBindingAsync(options, cancellationToken);
        await EnsureAutomationRoleBindingAsync(options, cancellationToken);

        Logger.LogInformation(
            "Successfully seeded Aspire local test administrator IAM principal {Email} with {RoleId}.",
            options.Email,
            options.RoleId);
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
}
