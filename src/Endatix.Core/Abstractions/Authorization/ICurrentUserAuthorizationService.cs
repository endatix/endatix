using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions.Authorization;

/// <summary>
/// Service contract for current user authorization data resolution and authorization logic for the current user.
/// </summary>
public interface ICurrentUserAuthorizationService
{
    /// <summary>
    /// Gets authorization data for the current user.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Authorization data for the current user.</returns>
    Task<Result<AuthorizationData>> GetAuthorizationDataAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Checks if the current user has a specific permission.
    /// Admin users always return true without any further checks.
    /// </summary>
    /// <param name="permission">The permission to check for.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if user has the permission, false otherwise.</returns>
    Task<Result<bool>> HasPermissionAsync(string permission, CancellationToken cancellationToken);

    /// <summary>
    /// Batch permission check for multiple permissions at once. More efficient than multiple individual calls.
    /// Admin users always return true for all permissions without any further checks.
    /// </summary>
    /// <param name="permissions">Collection of permissions to check.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Dictionary mapping permission names to boolean results.</returns>
    Task<Result<Dictionary<string, bool>>> HasPermissionsAsync(IEnumerable<string> permissions, CancellationToken cancellationToken);

    /// <summary>
    /// Checks if the current user is a tenant-level administrator.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if user is an admin, false otherwise.</returns>
    Task<Result<bool>> IsAdminAsync(CancellationToken cancellationToken);


    /// <summary>
    /// Checks if user is a platform-level administrator (cross-tenant access).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if user is a platform admin, false otherwise.</returns>
    Task<Result<bool>> IsPlatformAdminAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Validates whether the current user has the required permission. Checks authentication, admin status, and specific permission.
    /// </summary>
    /// <param name="requiredPermission">The permission required for access.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Success if access is granted, Unauthorized if not authenticated, Forbidden if lacking permission.</returns>
    Task<Result> ValidateAccessAsync(string requiredPermission, CancellationToken cancellationToken);

    /// <summary>
    /// Invalidates the authorization data cache for the current user.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task InvalidateAuthorizationDataCacheAsync(CancellationToken cancellationToken);
}
