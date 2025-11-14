using System.Security.Claims;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions.Authorization;

/// <summary>
/// Provides caching operations for authorization data.
/// Encapsulates cache key generation, expiration computation, and tag management.
/// </summary>
public interface IAuthorizationCache
{
    /// <summary>
    /// Gets or creates authorization data for a claims principal.
    /// Automatically computes cache key, expiration, and tags from the principal.
    /// Enriches the data with cache metadata (CachedAt, CacheExpiresIn, ETag) when storing.
    /// </summary>
    Task<AuthorizationData> GetOrCreateAsync(
        ClaimsPrincipal principal,
        Func<CancellationToken, Task<Result<AuthorizationData>>> dataFactory,
        CancellationToken cancellationToken);

    /// <summary>
    /// Gets or creates authorization data for a user with explicit userId and tenantId.
    /// Uses default expiration when principal is not available.
    /// </summary>
    Task<AuthorizationData> GetOrCreateAsync(
        string userId,
        long tenantId,
        ClaimsPrincipal? principal,
        Func<CancellationToken, Task<Result<AuthorizationData>>> dataFactory,
        CancellationToken cancellationToken);

    /// <summary>
    /// Invalidates authorization data cache for a specific user.
    /// </summary>
    Task InvalidateAsync(string userId, long tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Invalidates all authorization data caches.
    /// </summary>
    Task InvalidateAllAsync(CancellationToken cancellationToken);
}