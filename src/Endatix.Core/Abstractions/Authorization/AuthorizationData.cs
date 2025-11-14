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
        Roles = new HashSet<string>();
        Permissions = new HashSet<string>();
    }

    /// <summary>
    /// Private constructor for factory methods only. Ensures disciplined object creation.
    /// </summary>
    private AuthorizationData(string userId, long tenantId, string[] roles, string[] permissions, DateTime cachedAt, DateTime expiresAt, string eTag)
    {
        UserId = userId;
        TenantId = tenantId;
        Roles = roles.ToHashSet();
        Permissions = permissions.ToHashSet();
        IsAdmin = roles.Contains(SystemRole.Admin.Name) || roles.Contains(SystemRole.PlatformAdmin.Name);
        CachedAt = cachedAt;
        ExpiresAt = expiresAt;
        ETag = eTag;
    }

    public string UserId { get; init; }
    public long TenantId { get; init; }
    public HashSet<string> Roles { get; init; } = [];
    public HashSet<string> Permissions { get; init; } = [];
    public bool IsAdmin { get; init; }
    public DateTime CachedAt { get; init; }
    public DateTime ExpiresAt { get; init; }
    public string ETag { get; init; } = string.Empty;

    public static AuthorizationData ForAnonymousUser(long tenantId) => new(
        userId: "anonymous",
        tenantId: tenantId,
        roles: [SystemRole.Public.Name],
        permissions: SystemRole.Public.Permissions,
        cachedAt: DateTime.UtcNow,
        expiresAt: DateTime.UtcNow,
        eTag: string.Empty);

    public static AuthorizationData ForAuthenticatedUser(string userId, long tenantId, string[] roles, string[] permissions, DateTime cachedAt = default, DateTime expiresAt = default, string? eTag = null) => new(
        userId: userId,
        tenantId: tenantId,
        roles: [SystemRole.Authenticated.Name, .. roles],
        permissions: [.. SystemRole.Authenticated.Permissions, .. permissions],
        cachedAt: cachedAt,
        expiresAt: expiresAt,
        eTag: eTag ?? string.Empty);

    public AuthorizationData WithCacheMetadata(DateTime cachedAt, DateTime expiresAt, string eTag) =>
        this with
        {
            CachedAt = cachedAt,
            ExpiresAt = expiresAt,
            ETag = eTag
        };
}