using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Maliev.Aspire.ServiceDefaults.Authorization;

public class PermissionAuthorizationPolicyProvider : DefaultAuthorizationPolicyProvider
{
    public PermissionAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options) : base(options)
    {
    }

    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith("Permission:", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                // Policy Format: Permission:{permission}[:validate_model][:critical][:purpose_{text}]
                // Example: Permission:invoices.create:critical:purpose_Financial_Audit
                var content = policyName.Substring("Permission:".Length);

                if (string.IsNullOrWhiteSpace(content))
                {
                    throw new ArgumentException("Permission name cannot be empty", nameof(policyName));
                }

                var parts = content.Split(':');
                var permission = parts[0];

                bool validateModel = false;
                bool isCritical = false;
                string? auditPurpose = null;

                for (int i = 1; i < parts.Length; i++)
                {
                    if (parts[i].Equals("validate_model", StringComparison.OrdinalIgnoreCase))
                    {
                        validateModel = true;
                    }
                    else if (parts[i].Equals("critical", StringComparison.OrdinalIgnoreCase))
                    {
                        isCritical = true;
                    }
                    else if (parts[i].StartsWith("purpose_", StringComparison.OrdinalIgnoreCase))
                    {
                        auditPurpose = parts[i].Substring("purpose_".Length).Replace('_', ' ');
                    }
                    // Ignore unknown parts for forward compatibility
                }

                return new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermissionRequirement(
                        permission,
                        preValidateModel: validateModel,
                        isCritical: isCritical,
                        auditPurpose: auditPurpose))
                    .Build();
            }
            catch (Exception ex)
            {
                // Log error but don't crash - fall back to default policy provider
                // This prevents malformed policy names from breaking authorization
                throw new InvalidOperationException($"Failed to parse permission policy '{policyName}'. Format should be 'Permission:{{permission}}[:validate_model][:critical][:purpose_{{text}}]'", ex);
            }
        }

        return await base.GetPolicyAsync(policyName);
    }
}
