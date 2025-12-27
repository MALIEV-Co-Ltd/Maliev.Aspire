using Microsoft.AspNetCore.Authorization;

namespace Maliev.Aspire.ServiceDefaults.Authorization;

public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }
    public string? ResourcePathTemplate { get; }
    public bool RequireLiveCheck { get; }
    public bool PreValidateModel { get; }
    public bool IsCritical { get; }
    public string? AuditPurpose { get; }

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