using Maliev.Aspire.DatabaseSeeder.Seeding.Core;
using Maliev.IAMService.Data;
using Maliev.IAMService.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Maliev.Aspire.DatabaseSeeder.Seeding.Services.IAMService;

/// <summary>
/// Seeds IAM database with infrastructure data (system principals, roles, and bootstrap admin).
/// </summary>
public class IAMDatabaseSeeder : DatabaseSeeder<IAMDbContext>
{
    private const string PlatformOwnerRoleId = "roles.platform.owner";
    private static readonly Guid BootstrapAdminId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public IAMDatabaseSeeder(
        IAMDbContext context,
        ILogger<IAMDatabaseSeeder> logger)
        : base(context, logger)
    {
    }

    protected override async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        Logger.LogInformation("Seeding IAM infrastructure data...");

        await SeedSystemPrincipalAsync(cancellationToken);
        await SeedPlatformOwnerRoleAsync(cancellationToken);
        await SeedBootstrapAdminAsync(cancellationToken);

        Logger.LogInformation("IAM infrastructure seeding completed");
    }

    private async Task SeedSystemPrincipalAsync(CancellationToken ct)
    {
        var systemPrincipal = await Context.Principals
            .FirstOrDefaultAsync(p => p.Email == "system@maliev.internal", ct);

        if (systemPrincipal == null)
        {
            Logger.LogInformation("Creating system principal");
            systemPrincipal = new Principal
            {
                PrincipalId = Guid.Parse("00000000-0000-0000-0000-000000000001"),
                Email = "system@maliev.internal",
                DisplayName = "System Principal",
                PrincipalType = "system",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            Context.Principals.Add(systemPrincipal);
            await Context.SaveChangesAsync(ct);
        }
    }

    private async Task SeedPlatformOwnerRoleAsync(CancellationToken ct)
    {
        // T220: Ensure wildcard permission exists in database
        var wildcardPerm = await Context.Permissions.FirstOrDefaultAsync(p => p.PermissionId == "*", ct);
        if (wildcardPerm == null)
        {
            Logger.LogInformation("Creating wildcard permission");
            wildcardPerm = new Permission
            {
                PermissionId = "*",
                ServiceName = "platform",
                ResourceType = "all",
                Action = "all",
                Description = "Wildcard permission for full access"
            };
            Context.Permissions.Add(wildcardPerm);
            await Context.SaveChangesAsync(ct);
        }

        var role = await Context.Roles.FirstOrDefaultAsync(r => r.RoleId == PlatformOwnerRoleId, ct);
        if (role == null)
        {
            Logger.LogInformation("Creating Platform Owner role");
            role = new Role
            {
                RoleId = PlatformOwnerRoleId,
                RoleName = "Platform Owner",
                ServiceName = "platform",
                Description = "Full administrative access",
                IsCustom = false,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                RolePermissions = new List<RolePermission>
                {
                    new RolePermission { RoleId = PlatformOwnerRoleId, PermissionId = "*" }
                }
            };
            Context.Roles.Add(role);
            await Context.SaveChangesAsync(ct);
        }
    }

    private async Task SeedBootstrapAdminAsync(CancellationToken ct)
    {
        var admin = await Context.Principals.FirstOrDefaultAsync(p => p.PrincipalId == BootstrapAdminId, ct);
        if (admin == null)
        {
            Logger.LogInformation("Creating bootstrap admin principal {PrincipalId}", BootstrapAdminId);
            admin = new Principal
            {
                PrincipalId = BootstrapAdminId,
                Email = "admin@maliev.com",
                DisplayName = "System Admin",
                PrincipalType = "user",
                IsActive = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            Context.Principals.Add(admin);

            // Bind to role
            Context.PrincipalRoleBindings.Add(new PrincipalRoleBinding
            {
                BindingId = Guid.NewGuid(),
                PrincipalId = BootstrapAdminId,
                RoleId = PlatformOwnerRoleId,
                ResourcePath = "*",
                GrantedAt = DateTime.UtcNow
            });

            await Context.SaveChangesAsync(ct);
            Logger.LogInformation("Bootstrap admin principal created and role assigned");
        }
    }
}
