namespace Maliev.Aspire.ServiceDefaults.IAM;

/// <summary>
/// Represents a permission for IAM registration.
/// Matches the PermissionDto expected by IAM service.
/// </summary>
public record PermissionRegistration
{
    /// <summary>
    /// Unique permission identifier in GCP format (e.g., "accounting.journal-entries.create")
    /// </summary>
    public required string PermissionId { get; init; }

    /// <summary>
    /// Human-readable description of what this permission allows
    /// </summary>
    public required string Description { get; init; }
}
