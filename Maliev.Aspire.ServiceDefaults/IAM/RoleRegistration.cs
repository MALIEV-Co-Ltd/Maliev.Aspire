namespace Maliev.Aspire.ServiceDefaults.IAM;

/// <summary>
/// Represents a role for IAM registration.
/// Matches the RoleDto expected by IAM service.
/// </summary>
public record RoleRegistration
{
    /// <summary>
    /// Unique role identifier in GCP format (e.g., "roles.accounting.admin")
    /// </summary>
    public required string RoleId { get; init; }

    /// <summary>
    /// Human-readable description of what this role provides
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// List of permission IDs granted by this role
    /// </summary>
    public required List<string> PermissionIds { get; init; }

    /// <summary>
    /// Whether this is a custom role (default: false for predefined roles)
    /// </summary>
    public bool IsCustom { get; init; } = false;
}
