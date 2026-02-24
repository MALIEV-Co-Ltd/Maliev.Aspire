using Microsoft.AspNetCore.Authorization;

namespace Maliev.Aspire.ServiceDefaults.Authorization;

/// <summary>
/// Declarative permission-based authorization for controller actions.
/// Supports both global and resource-scoped permissions.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class RequirePermissionAttribute : AuthorizeAttribute
{
    private string _permission;
    private string? _resourcePathTemplate;
    private bool _requireLiveCheck;
    private bool _isCritical;
    private string? _auditPurpose;
    private bool _preValidateModel;

    /// <summary>Gets the required permission.</summary>
    public string Permission
    {
        get => _permission;
        init { _permission = value; UpdatePolicy(); }
    }

    /// <summary>Gets or sets the resource path template.</summary>
    public string? ResourcePathTemplate
    {
        get => _resourcePathTemplate;
        set { _resourcePathTemplate = value; UpdatePolicy(); }
    }

    /// <summary>Gets or sets a value indicating whether a live check is required.</summary>
    public bool RequireLiveCheck
    {
        get => _requireLiveCheck;
        set { _requireLiveCheck = value; UpdatePolicy(); }
    }

    /// <summary>Gets or sets a value indicating whether the requirement is critical.</summary>
    public bool IsCritical
    {
        get => _isCritical;
        set { _isCritical = value; UpdatePolicy(); }
    }

    /// <summary>Gets or sets the audit purpose.</summary>
    public string? AuditPurpose
    {
        get => _auditPurpose;
        set { _auditPurpose = value; UpdatePolicy(); }
    }

    /// <summary>Gets or sets a value indicating whether to pre-validate the model.</summary>
    public bool PreValidateModel
    {
        get => _preValidateModel;
        set { _preValidateModel = value; UpdatePolicy(); }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="RequirePermissionAttribute"/> class.
    /// </summary>
    /// <param name="permission">The required permission.</param>
    public RequirePermissionAttribute(string permission)
    {
        _permission = permission ?? throw new ArgumentNullException(nameof(permission));
        UpdatePolicy();
    }

    private void UpdatePolicy()
    {
        var basePolicy = _permission.StartsWith("Permission:", StringComparison.OrdinalIgnoreCase)
            ? _permission
            : $"Permission:{_permission}";

        var sb = new System.Text.StringBuilder(basePolicy);

        if (_preValidateModel) sb.Append(":validate_model");
        if (_isCritical) sb.Append(":critical");
        if (!string.IsNullOrEmpty(_auditPurpose))
        {
            // Sanitize audit purpose for policy name compatibility
            // Only allow alphanumeric, spaces (converted to underscores), hyphens, and periods
            var sanitized = SanitizeForPolicyName(_auditPurpose);
            sb.Append(":purpose_").Append(sanitized);
        }

        base.Policy = sb.ToString();
    }

    /// <summary>
    /// Sanitizes a string for safe use in policy names.
    /// Only allows alphanumeric characters, underscores, hyphens, and periods.
    /// Replaces spaces with underscores and removes all other special characters.
    /// </summary>
    private static string SanitizeForPolicyName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var result = new System.Text.StringBuilder(value.Length);

        foreach (char c in value)
        {
            if (char.IsLetterOrDigit(c) || c == '-' || c == '.')
            {
                result.Append(c);
            }
            else if (c == ' ')
            {
                result.Append('_');
            }
            // Ignore all other special characters (colons, quotes, etc.)
        }

        return result.ToString();
    }
}
