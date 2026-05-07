namespace Maliev.Aspire.Tests.Unit;

/// <summary>
/// Source guards for the Aspire-local automation IAM seeder.
/// </summary>
public class AspireTestAdminIamSeederSourceTests
{
    /// <summary>
    /// The automation principal must use its own wildcard role instead of the human Platform Owner role.
    /// </summary>
    [Fact]
    public void IamSeeder_AssignsDedicatedAutomationRoleAndRemovesLegacyPlatformOwnerBinding()
    {
        var source = File.ReadAllText(FindSeederSource());
        var optionsSource = File.ReadAllText(FindOptionsSource());

        Assert.Contains("AutomationRoleId = \"roles.aspire.automation\"", optionsSource, StringComparison.Ordinal);
        Assert.Contains("await EnsureAutomationRoleAsync(options, cancellationToken);", source, StringComparison.Ordinal);
        Assert.Contains("await RemoveLegacyPlatformOwnerBindingAsync(options, cancellationToken);", source, StringComparison.Ordinal);
        Assert.Contains("await EnsureAutomationRoleBindingAsync(options, cancellationToken);", source, StringComparison.Ordinal);
        Assert.Contains("RoleId = options.RoleId", source, StringComparison.Ordinal);
        Assert.Contains("PermissionId = \"*\"", source, StringComparison.Ordinal);
        Assert.Contains("b.RoleId == PlatformOwnerRoleId", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RoleId = PlatformOwnerRoleId,", source, StringComparison.Ordinal);
    }

    private static string FindSeederSource()
    {
        foreach (var startDirectory in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var directory = new DirectoryInfo(startDirectory);
            while (directory is not null)
            {
                var candidate = Path.Combine(
                    directory.FullName,
                    "Maliev.Aspire.DatabaseSeeder",
                    "Seeding",
                    "Services",
                    "IAMService",
                    "IAMDatabaseSeeder.cs");

                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        throw new FileNotFoundException("Unable to locate IAMDatabaseSeeder.cs.");
    }

    private static string FindOptionsSource()
    {
        foreach (var startDirectory in new[] { AppContext.BaseDirectory, Environment.CurrentDirectory })
        {
            var directory = new DirectoryInfo(startDirectory);
            while (directory is not null)
            {
                var candidate = Path.Combine(
                    directory.FullName,
                    "Maliev.Aspire.DatabaseSeeder",
                    "Seeding",
                    "Services",
                    "Shared",
                    "AspireTestAdminSeedOptions.cs");

                if (File.Exists(candidate))
                {
                    return candidate;
                }

                directory = directory.Parent;
            }
        }

        throw new FileNotFoundException("Unable to locate AspireTestAdminSeedOptions.cs.");
    }
}
