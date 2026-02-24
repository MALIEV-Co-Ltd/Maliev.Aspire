namespace Maliev.Aspire.ServiceDefaults.IAM;

/// <summary>
/// Utility class for matching required permissions against a set of user permission claims.
/// </summary>
public static class PermissionMatcher
{
    /// <summary>
    /// Checks if any of the user permissions match the required permission.
    /// </summary>
    /// <param name="requiredPermission">The permission required to access the resource.</param>
    /// <param name="userPermissions">The user's set of permission claims.</param>
    /// <returns>True if the user has the required permission; otherwise, false.</returns>
    public static bool Match(string requiredPermission, IEnumerable<string> userPermissions)
    {
        if (string.IsNullOrWhiteSpace(requiredPermission)) return false;
        if (userPermissions == null || !userPermissions.Any())
        {
            return false;
        }

        var permissionsList = userPermissions.ToList();

        // T220: Platform Owner bypass (standard for Maliev platform)
        if (permissionsList.Any(p => string.Equals(p, "roles.platform.owner", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        return permissionsList.Any(p => IsMatch(requiredPermission, p));
    }

    /// <summary>
    /// Performs a single comparison between a required permission and a claim string.
    /// Supports hierarchical matching with wildcards (e.g., "invoices.*").
    /// </summary>
    /// <param name="required">The required permission.</param>
    /// <param name="claim">The user's permission claim.</param>
    /// <returns>True if they match; otherwise, false.</returns>
    public static bool IsMatch(string required, string claim)
    {
        if (string.IsNullOrWhiteSpace(required) || string.IsNullOrWhiteSpace(claim)) return false;

        // Use spans to avoid Substring allocations
        var requiredSpan = required.AsSpan();
        if (requiredSpan.StartsWith("Permission:", StringComparison.OrdinalIgnoreCase))
        {
            requiredSpan = requiredSpan.Slice("Permission:".Length);
        }

        var claimSpan = claim.AsSpan();
        if (claimSpan.StartsWith("Permission:", StringComparison.OrdinalIgnoreCase))
        {
            claimSpan = claimSpan.Slice("Permission:".Length);
        }

        if (requiredSpan.Equals(claimSpan, StringComparison.OrdinalIgnoreCase)) return true;

        // For splitting and wildcard matching, we still use string parts for now 
        // as Span-based splitting is more complex in .NET 8/9 without extra helpers
        var requiredParts = requiredSpan.ToString().Split('.');
        var claimParts = claimSpan.ToString().Split('.');

        // Special case: claim is just "*"
        if (claimParts.Length == 1 && claimParts[0] == "*") return true;

        // SECURE WILDCARD MATCHING LOGIC


        // Security fix: Validate segments BEFORE wildcard, ensure wildcard only at end
        // Prevents bypasses like "*.delete" matching "invoices.create"
        for (int i = 0; i < claimParts.Length; i++)
        {
            if (claimParts[i] == "*")
            {
                // Wildcard found - enforce security rules:
                // 1. Wildcard must be the LAST segment (no "invoices.*.read" or "*.delete")
                // 2. All preceding segments must have matched to reach this point
                if (i != claimParts.Length - 1)
                {
                    // Security: Reject wildcard in the middle (e.g., "invoices.*.read")
                    // Wildcards only allowed as suffix (e.g., "invoices.*")
                    return false;
                }

                // Wildcard is at the end and all previous segments matched
                // This allows "invoices.*" to match "invoices.create", "invoices.read", etc.
                return true;
            }

            // Not a wildcard - validate this segment matches
            if (i >= requiredParts.Length)
            {
                // Claim has more non-wildcard segments than required permission
                return false;
            }

            if (!string.Equals(requiredParts[i], claimParts[i], StringComparison.OrdinalIgnoreCase))
            {
                // Segment mismatch - permission denied
                return false;
            }
        }

        // All claim segments processed and matched - lengths must be equal
        return claimParts.Length == requiredParts.Length;
    }
}
