using Microsoft.AspNetCore.Authorization;

namespace Maliev.Aspire.ServiceDefaults.Authorization;

/// <summary>
/// Declarative permission-based authorization for controller actions.
/// Supports both global and resource-scoped permissions.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class RequirePermissionAttribute : AuthorizeAttribute
{
    public string Permission { get; }
    public string? ResourcePathTemplate { get; set; }
    public bool RequireLiveCheck { get; set; }
    public bool IsCritical { get; set; }
    public string? AuditPurpose { get; set; }

            public bool PreValidateModel { get; set; }
        
            public new string? Policy
            {
                get
                {
                    var basePolicy = Permission.StartsWith("Permission:", StringComparison.OrdinalIgnoreCase)
                        ? Permission
                        : $"Permission:{Permission}";
        
                    return PreValidateModel ? $"{basePolicy}:validate_model" : basePolicy;
                }
                set => base.Policy = value;
            }    
        public RequirePermissionAttribute(string permission)
        {
            Permission = permission ?? throw new ArgumentNullException(nameof(permission));
        }}