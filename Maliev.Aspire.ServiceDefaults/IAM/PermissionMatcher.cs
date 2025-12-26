namespace Maliev.Aspire.ServiceDefaults.IAM;

public static class PermissionMatcher
{
    public static bool Match(string requiredPermission, IEnumerable<string> userPermissions)
    {
        if (string.IsNullOrWhiteSpace(requiredPermission)) return false;
        if (userPermissions == null || !userPermissions.Any())
        {
            return false;
        }

        return userPermissions.Any(p => IsMatch(requiredPermission, p));
    }

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
    
            // FLEXIBLE MATCHING LOGIC
            // Match segment by segment
            bool match = true;
            for (int i = 0; i < claimParts.Length; i++)
            {
                if (claimParts[i] == "*")
                {
                    // Wildcard matches everything from this point on
                    return true;
                }
    
                if (i >= requiredParts.Length || !string.Equals(requiredParts[i], claimParts[i], StringComparison.OrdinalIgnoreCase))       
                {
                    match = false;
                    break;
                }
            }
    
            return match && claimParts.Length == requiredParts.Length;
        }}
