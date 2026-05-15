using Microsoft.Extensions.Configuration;

namespace Maliev.Aspire.DatabaseSeeder.Seeding.Services.Shared;

/// <summary>
/// Configuration for the Aspire-local test administrator seed.
/// </summary>
public sealed class AspireTestAdminSeedOptions
{
    private const string DefaultEmail = "aspire-automation@debug.com";
    private const string ReservedSystemPrincipalEmail = "system@maliev.com";

    /// <summary>
    /// Configuration marker used to distinguish the synthetic local test administrator from real employees.
    /// </summary>
    public const string LinkedServiceName = "AspireTestAdminSeeder";

    /// <summary>
    /// IAM role assigned to the Aspire-local automation principal.
    /// </summary>
    public const string AutomationRoleId = "roles.aspire.automation";

    /// <summary>
    /// IAM role assigned to the Aspire-local limited-permission employee principal.
    /// </summary>
    public const string LimitedRoleId = "roles.aspire.limited";

    private static readonly Guid DefaultPrincipalId = Guid.Parse("11111111-1111-1111-1111-111111111001");
    private static readonly Guid DefaultEmployeeId = Guid.Parse("11111111-1111-1111-1111-111111111002");
    private static readonly Guid DefaultLimitedPrincipalId = Guid.Parse("11111111-1111-1111-1111-111111111101");
    private static readonly Guid DefaultLimitedEmployeeId = Guid.Parse("11111111-1111-1111-1111-111111111102");

    /// <summary>
    /// Gets a value indicating whether the Aspire-local test administrator seed is enabled.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Gets the synthetic employee email used for password login.
    /// </summary>
    public string Email { get; init; } = DefaultEmail;

    /// <summary>
    /// Gets the local-only password supplied from user secrets or environment variables.
    /// </summary>
    public string? Password { get; init; }

    /// <summary>
    /// Gets the stable IAM principal identifier used by the local test administrator.
    /// </summary>
    public Guid PrincipalId { get; init; } = DefaultPrincipalId;

    /// <summary>
    /// Gets the stable employee identifier used by the local test administrator.
    /// </summary>
    public Guid EmployeeId { get; init; } = DefaultEmployeeId;

    /// <summary>
    /// Gets the employee number assigned to the local test administrator.
    /// </summary>
    public string EmployeeNumber { get; init; } = "EMP-CODEX-001";

    /// <summary>
    /// Gets the first name assigned to the local test administrator.
    /// </summary>
    public string FirstName { get; init; } = "Codex";

    /// <summary>
    /// Gets the last name assigned to the local test administrator.
    /// </summary>
    public string LastName { get; init; } = "Admin";

    /// <summary>
    /// Gets the preferred display name assigned to the local test administrator.
    /// </summary>
    public string PreferredName { get; init; } = "Codex Admin";

    /// <summary>
    /// Gets the IAM linked-service marker used to exclude this seed from first-real-user bootstrap counts.
    /// </summary>
    public string LinkedService { get; init; } = LinkedServiceName;

    /// <summary>
    /// Gets the dedicated IAM role assigned to the local automation principal.
    /// </summary>
    public string RoleId { get; init; } = AutomationRoleId;

    /// <summary>
    /// Gets the synthetic limited employee email used for permission-boundary browser E2E tests.
    /// </summary>
    public string LimitedEmail { get; init; } = "aspire-limited@debug.com";

    /// <summary>
    /// Gets the stable IAM principal identifier used by the limited employee.
    /// </summary>
    public Guid LimitedPrincipalId { get; init; } = DefaultLimitedPrincipalId;

    /// <summary>
    /// Gets the stable employee identifier used by the limited employee.
    /// </summary>
    public Guid LimitedEmployeeId { get; init; } = DefaultLimitedEmployeeId;

    /// <summary>
    /// Gets the employee number assigned to the limited employee.
    /// </summary>
    public string LimitedEmployeeNumber { get; init; } = "EMP-CODEX-002";

    /// <summary>
    /// Gets the first name assigned to the limited employee.
    /// </summary>
    public string LimitedFirstName { get; init; } = "Codex";

    /// <summary>
    /// Gets the last name assigned to the limited employee.
    /// </summary>
    public string LimitedLastName { get; init; } = "Limited";

    /// <summary>
    /// Gets the preferred display name assigned to the limited employee.
    /// </summary>
    public string LimitedPreferredName { get; init; } = "Codex Limited";

    /// <summary>
    /// Gets the dedicated IAM role assigned to the limited employee.
    /// </summary>
    public string LimitedRoleIdValue { get; init; } = LimitedRoleId;

    /// <summary>
    /// Gets the permissions intentionally granted to the limited employee.
    /// </summary>
    public IReadOnlyList<string> LimitedRolePermissions { get; init; } =
    [
        "auth.sessions.read",
        "employee.profiles.read",
        "employee.profiles.update"
    ];

    /// <summary>
    /// Creates options from configuration and validates fail-closed safety rules.
    /// </summary>
    /// <param name="configuration">The application configuration.</param>
    /// <returns>Parsed Aspire test administrator options.</returns>
    public static AspireTestAdminSeedOptions FromConfiguration(IConfiguration configuration)
    {
        var enabled = bool.TryParse(configuration["AspireTestAdmin:Enabled"], out var parsedEnabled) && parsedEnabled;

        var options = new AspireTestAdminSeedOptions
        {
            Enabled = enabled,
            Email = ReadString(configuration, "AspireTestAdmin:Email", DefaultEmail),
            Password = ReadOptionalString(configuration, "AspireTestAdmin:Password"),
            PrincipalId = ReadGuid(configuration, "AspireTestAdmin:PrincipalId", DefaultPrincipalId),
            EmployeeId = ReadGuid(configuration, "AspireTestAdmin:EmployeeId", DefaultEmployeeId),
            EmployeeNumber = ReadString(configuration, "AspireTestAdmin:EmployeeNumber", "EMP-CODEX-001"),
            FirstName = ReadString(configuration, "AspireTestAdmin:FirstName", "Codex"),
            LastName = ReadString(configuration, "AspireTestAdmin:LastName", "Admin"),
            PreferredName = ReadString(configuration, "AspireTestAdmin:PreferredName", "Codex Admin"),
            LinkedService = LinkedServiceName,
            RoleId = AutomationRoleId,
            LimitedEmail = ReadString(configuration, "AspireTestAdmin:LimitedEmail", "aspire-limited@debug.com"),
            LimitedPrincipalId = ReadGuid(configuration, "AspireTestAdmin:LimitedPrincipalId", DefaultLimitedPrincipalId),
            LimitedEmployeeId = ReadGuid(configuration, "AspireTestAdmin:LimitedEmployeeId", DefaultLimitedEmployeeId),
            LimitedEmployeeNumber = ReadString(configuration, "AspireTestAdmin:LimitedEmployeeNumber", "EMP-CODEX-002"),
            LimitedFirstName = ReadString(configuration, "AspireTestAdmin:LimitedFirstName", "Codex"),
            LimitedLastName = ReadString(configuration, "AspireTestAdmin:LimitedLastName", "Limited"),
            LimitedPreferredName = ReadString(configuration, "AspireTestAdmin:LimitedPreferredName", "Codex Limited"),
            LimitedRoleIdValue = LimitedRoleId
        };

        if (options.Enabled && string.IsNullOrWhiteSpace(options.Password))
        {
            throw new InvalidOperationException(
                "AspireTestAdmin:Password is required when AspireTestAdmin:Enabled is true. " +
                "Set it through local user secrets or an environment variable; do not commit it.");
        }

        if (options.Enabled &&
            string.Equals(options.Email, ReservedSystemPrincipalEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"AspireTestAdmin:Email cannot be {ReservedSystemPrincipalEmail}. " +
                "That address is reserved for the IAM system principal; use a synthetic automation user instead.");
        }

        if (options.Enabled &&
            string.Equals(options.LimitedEmail, ReservedSystemPrincipalEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"AspireTestAdmin:LimitedEmail cannot be {ReservedSystemPrincipalEmail}. " +
                "That address is reserved for the IAM system principal; use a synthetic automation user instead.");
        }

        if (options.Enabled &&
            string.Equals(options.Email, options.LimitedEmail, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "AspireTestAdmin:LimitedEmail must be different from AspireTestAdmin:Email so permission-boundary tests use a separate principal.");
        }

        return options;
    }

    private static string ReadString(IConfiguration configuration, string key, string defaultValue)
    {
        var value = configuration[key];
        return string.IsNullOrWhiteSpace(value) ? defaultValue : value.Trim();
    }

    private static string? ReadOptionalString(IConfiguration configuration, string key)
    {
        var value = configuration[key];
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static Guid ReadGuid(IConfiguration configuration, string key, Guid defaultValue)
    {
        var value = configuration[key];
        return Guid.TryParse(value, out var parsed) ? parsed : defaultValue;
    }
}
