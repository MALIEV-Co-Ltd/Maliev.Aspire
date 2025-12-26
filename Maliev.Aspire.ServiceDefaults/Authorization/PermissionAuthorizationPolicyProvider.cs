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
            var content = policyName.Substring("Permission:".Length);
            var parts = content.Split(':');
            var permission = parts[0];
            bool validateModel = parts.Length > 1 && parts[1].Equals("validate_model", StringComparison.OrdinalIgnoreCase);
            
            return new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission, preValidateModel: validateModel))
                .Build();
        }

        return await base.GetPolicyAsync(policyName);
    }
}
