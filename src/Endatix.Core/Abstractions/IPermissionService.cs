using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions;

/// <summary>
/// Service contract for permission resolution and caching.
/// Provides efficient permission checking with multi-level caching strategy.
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Gets all effective permissions for a user (with caching).
    /// Admin users bypass database calls and return all permissions.
    /// </summary>
    /// <param name="userId">The user ID to get permissions for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of permission names the user has.</returns>
    Task<Result<IEnumerable<string>>> GetUserPermissionsAsync(long userId, CancellationToken cancellationToken = default);

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
    /// Checks if a user is an administrator.
    /// This is a high-performance method that checks admin status with minimal overhead.
    /// Used internally for admin bypass optimizations.
    /// </summary>
    /// <param name="userId">The user ID to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if user is an admin, false otherwise.</returns>
    Task<Result<bool>> IsUserAdminAsync(long userId, CancellationToken cancellationToken = default);

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
    /// Gets comprehensive user role and permission information for API responses.
    /// Includes caching metadata for client-side cache management.
    /// </summary>
    /// <param name="userId">The user ID to get role info for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Complete user role and permission information.</returns>
    Task<Result<UserRoleInfo>> GetUserRoleInfoAsync(long userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates whether the current user has the required permission.
    /// Checks authentication, admin status, and specific permission.
    /// </summary>
    /// <param name="userId">The user ID (can be null for anonymous users).</param>
    /// <param name="requiredPermission">The permission required for access.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success if access is granted, Unauthorized if not authenticated, Forbidden if lacking permission.</returns>
    Task<Result> ValidateAccessAsync(string? userId, string requiredPermission, CancellationToken cancellationToken = default);

    // Note: Advanced methods like batch operations, cache management, and statistics
    // can be added later when needed. Keeping interface minimal for now.
}

/// <summary>
/// User role and permission information for API responses.
/// Optimized for JSON serialization and client-side caching.
/// </summary>
public sealed class UserRoleInfo
{
    public long UserId { get; init; }
    public long TenantId { get; init; }
    public string[] Roles { get; init; } = [];
    public string[] Permissions { get; init; } = [];
    public bool IsAdmin { get; init; }
    public DateTime CachedAt { get; init; }
    public TimeSpan CacheExpiresIn { get; init; }
    
    /// <summary>
    /// ETag for cache validation in HTTP responses.
    /// </summary>
    public string ETag { get; init; } = string.Empty;
    
    /// <summary>
    /// Indicates if this data came from cache or was freshly computed.
    /// </summary>
    public bool FromCache { get; init; }
}

/// <summary>
/// Permission cache performance statistics.
/// </summary>
public sealed class PermissionCacheStats
{
    public long TotalRequests { get; init; }
    public long CacheHits { get; init; }
    public long CacheMisses { get; init; }
    public double HitRate => TotalRequests > 0 ? (double)CacheHits / TotalRequests : 0;
    public long ActiveUsers { get; init; }
    public long CachedRoles { get; init; }
    public TimeSpan AverageResponseTime { get; init; }
    public DateTime LastCacheInvalidation { get; init; }
}
