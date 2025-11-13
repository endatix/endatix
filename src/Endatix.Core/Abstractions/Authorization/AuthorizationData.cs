using System.Text.Json.Serialization;

namespace Endatix.Core.Abstractions.Authorization;



/// <summary>
/// Authorization data for a user including roles, permissions, admin status and metadata for caching.
/// Optimized for JSON serialization and client-side caching.
/// </summary>
public sealed record AuthorizationData
{
    /// <summary>
    /// Parameterless constructor for JSON deserialization only (used by HybridCache).
    /// Application code should use factory methods: <see cref="ForAnonymousUser"/> or <see cref="ForAuthenticatedUser"/>.
    /// Properties are init-only to maintain immutability after deserialization.
    /// </summary>
    [JsonConstructor]
    public AuthorizationData()
    {
        UserId = string.Empty;
        Roles = [];
        Permissions = [];
    }

    /// <summary>
    /// Private constructor for factory methods only. Ensures disciplined object creation.
    /// </summary>
    private AuthorizationData(string userId, long tenantId, string[] roles, string[] permissions, DateTime cachedAt, TimeSpan cacheExpiresIn, string eTag)
    {
        UserId = userId;
        TenantId = tenantId;
        Roles = roles;
        Permissions = permissions;
        IsAdmin = roles.Contains(SystemRole.Admin.Name) || roles.Contains(SystemRole.PlatformAdmin.Name);
        CachedAt = cachedAt;
        CacheExpiresIn = cacheExpiresIn;
        ETag = eTag;
    }

    public string UserId { get; init; }
    public long TenantId { get; init; }
    public string[] Roles { get; init; } = [];
    public string[] Permissions { get; init; } = [];
    public bool IsAdmin { get; init; }
    public DateTime CachedAt { get; init; }
    public TimeSpan CacheExpiresIn { get; init; }
    public string ETag { get; init; } = string.Empty;

    public static AuthorizationData ForAnonymousUser(long tenantId) => new("anonymous", tenantId, [], [], DateTime.UtcNow, TimeSpan.Zero, string.Empty);

    public static AuthorizationData ForAuthenticatedUser(string userId, long tenantId, string[] roles, string[] permissions, DateTime cachedAt, TimeSpan cacheExpiresIn, string eTag) => new(userId, tenantId, roles, permissions, cachedAt, cacheExpiresIn, eTag);
}