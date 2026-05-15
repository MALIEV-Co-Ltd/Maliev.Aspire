using Maliev.Aspire.DatabaseSeeder.Seeding.Services.Shared;
using Microsoft.Extensions.Configuration;

namespace Maliev.Aspire.Tests.Unit;

/// <summary>
/// Tests for Aspire-local test administrator seed configuration.
/// </summary>
public class AspireTestAdminSeedOptionsTests
{
    /// <summary>
    /// Enabled seeding requires a password supplied by local configuration or user secrets.
    /// </summary>
    [Fact]
    public void FromConfiguration_WhenEnabledWithoutPassword_Throws()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["AspireTestAdmin:Enabled"] = "true"
        });

        var exception = Assert.Throws<InvalidOperationException>(
            () => AspireTestAdminSeedOptions.FromConfiguration(configuration));

        Assert.Contains("AspireTestAdmin:Password", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    /// Disabled seeding must not require or invent a password.
    /// </summary>
    [Fact]
    public void FromConfiguration_WhenDisabled_DoesNotRequirePassword()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["AspireTestAdmin:Enabled"] = "false"
        });

        var options = AspireTestAdminSeedOptions.FromConfiguration(configuration);

        Assert.False(options.Enabled);
        Assert.Null(options.Password);
    }

    /// <summary>
    /// Defaults should use a synthetic local identity and stable IDs for repeatable Aspire seeding.
    /// </summary>
    [Fact]
    public void FromConfiguration_WhenEnabled_UsesSyntheticDefaults()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["AspireTestAdmin:Enabled"] = "true",
            ["AspireTestAdmin:Password"] = "local-only-secret"
        });

        var options = AspireTestAdminSeedOptions.FromConfiguration(configuration);

        Assert.True(options.Enabled);
        Assert.Equal("aspire-automation@debug.com", options.Email);
        Assert.Equal("AspireTestAdminSeeder", options.LinkedService);
        Assert.Equal("roles.aspire.automation", options.RoleId);
        Assert.Equal("aspire-limited@debug.com", options.LimitedEmail);
        Assert.Equal("roles.aspire.limited", options.LimitedRoleIdValue);
        Assert.Contains("auth.sessions.read", options.LimitedRolePermissions);
        Assert.Contains("employee.profiles.read", options.LimitedRolePermissions);
        Assert.Contains("employee.profiles.update", options.LimitedRolePermissions);
        Assert.NotEqual(Guid.Empty, options.PrincipalId);
        Assert.NotEqual(Guid.Empty, options.EmployeeId);
        Assert.NotEqual(Guid.Empty, options.LimitedPrincipalId);
        Assert.NotEqual(Guid.Empty, options.LimitedEmployeeId);
    }

    /// <summary>
    /// The system principal is reserved for service-to-service work and must not be reused as a browser login.
    /// </summary>
    [Fact]
    public void FromConfiguration_WhenEmailIsSystemPrincipal_Throws()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["AspireTestAdmin:Enabled"] = "true",
            ["AspireTestAdmin:Password"] = "local-only-secret",
            ["AspireTestAdmin:Email"] = "system@maliev.com"
        });

        var exception = Assert.Throws<InvalidOperationException>(
            () => AspireTestAdminSeedOptions.FromConfiguration(configuration));

        Assert.Contains("system@maliev.com", exception.Message, StringComparison.Ordinal);
        Assert.Contains("reserved", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// The limited employee must be a separate principal so negative permission checks are meaningful.
    /// </summary>
    [Fact]
    public void FromConfiguration_WhenLimitedEmailMatchesAdminEmail_Throws()
    {
        var configuration = BuildConfiguration(new Dictionary<string, string?>
        {
            ["AspireTestAdmin:Enabled"] = "true",
            ["AspireTestAdmin:Password"] = "local-only-secret",
            ["AspireTestAdmin:Email"] = "same@debug.com",
            ["AspireTestAdmin:LimitedEmail"] = "same@debug.com"
        });

        var exception = Assert.Throws<InvalidOperationException>(
            () => AspireTestAdminSeedOptions.FromConfiguration(configuration));

        Assert.Contains("LimitedEmail", exception.Message, StringComparison.Ordinal);
        Assert.Contains("different", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values)
    {
        return new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
    }
}
