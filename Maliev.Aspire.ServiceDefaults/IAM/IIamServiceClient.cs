namespace Maliev.Aspire.ServiceDefaults.IAM;

/// <summary>
/// Client interface for interacting with the central IAM service.
/// Supports both global and resource-scoped permission checking with caching.
/// </summary>
public interface IIamServiceClient
{
    /// <summary>
    /// Gets all active permissions for a user (global permissions only).
    /// Results are cached in the IAM service with 5-minute TTL.
    /// </summary>
    /// <param name="userId">The user's principal ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A collection of permission strings (e.g., "order.orders.create").</returns>
    Task<IEnumerable<string>> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a principal has a specific permission, optionally scoped to a resource path.
    /// This is the primary method for resource-scoped authorization checks.
    /// Target latency: &lt;10ms (cached), &lt;50ms (uncached).
    /// </summary>
    /// <param name="principalId">The principal ID (user or service account).</param>
    /// <param name="permissionId">The permission to check (e.g., "order.orders.read").</param>
    /// <param name="resourcePath">Optional hierarchical resource path (e.g., "customers/123/orders/456").</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the principal has the permission (globally or on the specified resource).</returns>
    Task<bool> CheckPermissionAsync(
        string principalId,
        string permissionId,
        string? resourcePath = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks multiple permissions in a single call for better performance.
    /// Useful for complex authorization scenarios requiring multiple permission checks.
    /// </summary>
    /// <param name="principalId">The principal ID.</param>
    /// <param name="requests">List of permission checks to perform.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping permission IDs to boolean results.</returns>
    Task<Dictionary<string, bool>> CheckPermissionsAsync(
        string principalId,
        IEnumerable<PermissionCheckRequest> requests,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Request for a single permission check (used in bulk checks).
/// </summary>
public record PermissionCheckRequest
{
    /// <summary>
    /// The permission ID to check.
    /// </summary>
    public required string PermissionId { get; init; }

    /// <summary>
    /// Optional resource path for scoped checking.
    /// </summary>
    public string? ResourcePath { get; init; }
}
