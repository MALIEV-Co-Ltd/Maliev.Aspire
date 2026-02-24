using Microsoft.AspNetCore.Authorization;

namespace Maliev.Aspire.ServiceDefaults.Authorization;

/// <summary>
/// Represents an authorization requirement for a specific permission.
/// </summary>
public class PermissionRequirement : IAuthorizationRequirement
{
    /// <summary>Gets the required permission string.</summary>
    public string Permission { get; }
    /// <summary>Gets the optional resource path template for granular authorization.</summary>
    public string? ResourcePathTemplate { get; }
    /// <summary>Gets a value indicating whether a live check against the IAM service is required.</summary>
    public bool RequireLiveCheck { get; }
    /// <summary>Gets a value indicating whether pre-validation of the model is required.</summary>
    public bool PreValidateModel { get; }
    /// <summary>Gets a value indicating whether this is a critical authorization check.</summary>
    public bool IsCritical { get; }
    /// <summary>Gets the optional audit purpose text.</summary>
    public string? AuditPurpose { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionRequirement"/> class.
    /// </summary>
    /// <param name="permission">The required permission.</param>
    /// <param name="resourcePathTemplate">The optional resource path template.</param>
    /// <param name="requireLiveCheck">Whether a live check is required.</param>
    /// <param name="preValidateModel">Whether to pre-validate the model.</param>
    /// <param name="isCritical">Whether the requirement is critical.</param>
    /// <param name="auditPurpose">The optional audit purpose.</param>
    public PermissionRequirement(
        string permission,
        string? resourcePathTemplate = null,
        bool requireLiveCheck = false,
        bool preValidateModel = false,
        bool isCritical = false,
        string? auditPurpose = null)
    {
        Permission = permission ?? throw new ArgumentNullException(nameof(permission));
        ResourcePathTemplate = resourcePathTemplate;
        RequireLiveCheck = requireLiveCheck;
        PreValidateModel = preValidateModel;
        IsCritical = isCritical;
        AuditPurpose = auditPurpose;
    }
}