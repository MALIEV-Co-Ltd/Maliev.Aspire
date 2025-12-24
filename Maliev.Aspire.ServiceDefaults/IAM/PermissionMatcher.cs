namespace Maliev.Aspire.ServiceDefaults.IAM;

internal static class PermissionMatcher
{
    public static bool Match(string requiredPermission, IEnumerable<string> userPermissions)
    {
        if (string.IsNullOrWhiteSpace(requiredPermission)) return false;
        if (userPermissions == null) return false;

        return userPermissions.Any(p => IsMatch(requiredPermission, p));
    }

    public static bool IsMatch(string required, string claim)
    {
        if (string.IsNullOrWhiteSpace(required) || string.IsNullOrWhiteSpace(claim)) return false;

        if (string.Equals(required, claim, StringComparison.OrdinalIgnoreCase)) return true;

        var requiredParts = required.Split('.');
        var claimParts = claim.Split('.');

        // Special case: claim is just "*"
        if (claimParts.Length == 1 && claimParts[0] == "*") return true;

        // If segment counts don't match, it can only match if the claim ends with * 
        // and all segments up to that point match.
        // But per spec, we expect 3 segments. 
        // Let's implement flexible segment matching.

        int maxSegments = Math.Max(requiredParts.Length, claimParts.Length);

        for (int i = 0; i < claimParts.Length; i++)
        {
            if (claimParts[i] == "*")
            {
                // If it's the last segment in the claim, it matches everything else in the requirement
                if (i == claimParts.Length - 1) return true;
                continue;
            }

            if (i >= requiredParts.Length) return false; // Claim is more specific than requirement

            if (!string.Equals(requiredParts[i], claimParts[i], StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // If we reached here, all claim segments matched. 
        // If claim had fewer segments but didn't end in *, it's not a full match.
        return claimParts.Length == requiredParts.Length;
    }
}