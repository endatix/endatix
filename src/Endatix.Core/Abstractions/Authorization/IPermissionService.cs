using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions.Authorization;

/// <summary>
/// Service contract for permission required data resolution and authorization logic.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Checks if a user has a specific permission (with caching).
    /// Optimized for single permission checks with fast cache lookup.
    /// Admin users always return true without database calls.
    /// </summary>
    /// <param name="userId">The user ID to check.</param>
    /// <param name="permission">The permission to check for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if user has the permission, false otherwise.</returns>
    Task<Result<bool>> HasPermissionAsync(long userId, string permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Batch permission check for multiple permissions at once.
    /// More efficient than multiple individual calls.
    /// </summary>
    /// <param name="userId">The user ID to check.</param>
    /// <param name="permissions">Collection of permissions to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping permission names to boolean results.</returns>
    Task<Result<Dictionary<string, bool>>> HasPermissionsAsync(long userId, IEnumerable<string> permissions, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a user is an tenant-level administrator.
    /// This is a high-performance method that checks admin status with minimal overhead.
    /// Used internally for admin bypass optimizations.
    /// </summary>
    /// <param name="userId">The user ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if user is an admin, false otherwise.</returns>
    Task<Result<bool>> IsUserAdminAsync(long userId, CancellationToken cancellationToken = default);


    /// <summary>
    /// Checks if user is a platform-level administrator (cross-tenant access).
    /// </summary>
    /// <param name="userId">The user ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if user is a platform admin, false otherwise.</returns>
    Task<Result<bool>> IsUserPlatformAdminAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets comprehensive user role and permission information for API responses.
    /// Includes caching metadata for client-side cache management.
    /// </summary>
    /// <param name="userId">The user ID to get role info for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete user role and permission information.</returns>
    Task<Result<AuthorizationData>> GetUserPermissionsInfoAsync(long userId, CancellationToken cancellationToken = default);


    /// <summary>
    /// Gets comprehensive user role and permission information for the current user.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete user role and permission information.</returns>
    Task<Result<AuthorizationData>> GetCurrentUserPermissionsInfoAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates whether the current user has the required permission.
    /// Checks authentication, admin status, and specific permission.
    /// </summary>
    /// <param name="userId">The user ID (can be null for anonymous users).</param>
    /// <param name="requiredPermission">The permission required for access.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success if access is granted, Unauthorized if not authenticated, Forbidden if lacking permission.</returns>
    Task<Result> ValidateAccessAsync(string? userId, string requiredPermission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Invalidates the user permission cache.
    /// </summary>
    /// <param name="userId">The user ID to invalidate the cache for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InvalidateUserPermissionCacheAsync(long userId, CancellationToken cancellationToken = default);
}
