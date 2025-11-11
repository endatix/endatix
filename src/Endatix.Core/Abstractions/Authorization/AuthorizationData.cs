namespace Endatix.Core.Abstractions.Authorization;



/// <summary>
/// Authorization data for a user including roles, permissions, admin status and metadata for caching.
/// Optimized for JSON serialization and client-side caching.
/// </summary>
public sealed class AuthorizationData
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