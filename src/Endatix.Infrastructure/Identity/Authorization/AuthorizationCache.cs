using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Caching.Hybrid;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Internal helper class for managing the authorization cache.
/// </summary>
/// <remarks>
/// Every key carries this deployment's prefix and the access versions of the request's tenant and of system roles,
/// read uncached on each lookup. A role change writes a new access version for its tenant as the last statement of its
/// transaction, so every API instance misses its old entries on the next request; <see cref="InvalidateAsync"/> only
/// frees memory on the instance it runs on. An entry lives at most 60 seconds and ends 10 seconds before its token (one
/// second at least), and is never served past the earliest expiry of the role permissions it holds.
/// </remarks>
internal class AuthorizationCache(
    HybridCache hybridCache,
    IDateTimeProvider dateTimeProvider,
    AuthorizationTokenFingerprint tokenFingerprint,
    IAccessVersionStore accessVersionStore,
    RequiredActionEvaluation actions) : IAuthorizationCache
{
    /// <summary>The longest an entry lives, which bounds a change that skips the save hook.</summary>
    private static readonly TimeSpan _maxEntryLifetime = TimeSpan.FromSeconds(60);

    private const short ETAG_LENGTH = 12;

    // Cache key prefixes
    private const string JWT_AUTH_DATA_CACHE_KEY_PREFIX = "jwt_auth";
    private const string USER_AUTH_DATA_CACHE_KEY_PREFIX = "usr_auth";

    // Cache tags
    private const string ALL_AUTH_DATA_TAG = "auth_data:all";

    private static readonly TimeSpan _cacheExpiryBuffer = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _minEntryLifetime = TimeSpan.FromSeconds(1);

    /// <inheritdoc />
    public async Task<AuthorizationData> GetOrCreateAsync(ClaimsPrincipal principal, Func<CancellationToken, Task<Result<AuthorizationData>>> dataFactory, CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId() ?? throw new InvalidOperationException("Principal must have a user ID");
        var tenantId = principal.GetTenantId();
        var cacheKeyTokenPart = tokenFingerprint.For(principal, userId, tenantId);
        var versions = await accessVersionStore.ReadAsync(tenantId, cancellationToken);

        return await GetOrCreateEntryAsync(
            new CacheEntry(GetClaimsCacheKey(versions, cacheKeyTokenPart), ComputeLifetime(principal), GetAuthDataCacheTags(userId, tenantId), versions, actions.ConfigurationKey, tenantId),
            dataFactory,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AuthorizationData> GetOrCreateAsync(string userId, long tenantId, ClaimsPrincipal? principal, Func<CancellationToken, Task<Result<AuthorizationData>>> dataFactory, CancellationToken cancellationToken)
    {
        var versions = await accessVersionStore.ReadAsync(tenantId, cancellationToken);

        return await GetOrCreateEntryAsync(
            new CacheEntry(GetUserAuthDataCacheKey(versions, userId, tenantId), ComputeLifetime(principal), GetAuthDataCacheTags(userId, tenantId), versions, actions.ConfigurationKey, tenantId),
            dataFactory,
            cancellationToken);
    }

    /// <summary>
    /// Serves the entry, building it on a miss. An entry past its own <see cref="AuthorizationData.ExpiresAt"/> (a role
    /// permission it holds has lapsed) counts as a miss: HybridCache fixes an entry's lifetime before the data is
    /// built, so the data carries the shorter bound itself.
    /// </summary>
    private async Task<AuthorizationData> GetOrCreateEntryAsync(
        CacheEntry entry,
        Func<CancellationToken, Task<Result<AuthorizationData>>> dataFactory,
        CancellationToken cancellationToken)
    {
        AuthorizationData? built = null;
        var cached = await hybridCache.GetOrCreateAsync(
            entry.Key,
            async _ => built = (await BuildAsync(dataFactory, entry, cancellationToken))(),
            entry.Options,
            entry.Tags,
            cancellationToken);
        // Report a fresh conflict once; on a cache hit, re-evaluate it rather than serving it as usable standing.
        var mustRehydrate = cached.RequiredActionsUnavailable || NeedsHydratedVersion(entry, cached);
        return cached.ExpiresAt > dateTimeProvider.Now.UtcDateTime && (!mustRehydrate || ReferenceEquals(cached, built))
            ? cached
            : await RebuildAsync(entry, dataFactory, cancellationToken);
    }

    /// <summary>Replaces an entry whose data has lapsed with freshly built data.</summary>
    private async Task<AuthorizationData> RebuildAsync(
        CacheEntry entry,
        Func<CancellationToken, Task<Result<AuthorizationData>>> dataFactory,
        CancellationToken cancellationToken)
    {
        var project = await BuildAsync(dataFactory, entry, cancellationToken);
        var rebuilt = project();
        await hybridCache.SetAsync(entry.Key, rebuilt, entry.Options, entry.Tags, cancellationToken);
        // If the write crossed a deadline, project each original report against its own policy at return time.
        return actions.HasProviders && rebuilt.ExpiresAt <= dateTimeProvider.Now.UtcDateTime ? project() : rebuilt;
    }

    /// <summary>Builds the data and stamps it with the time it stops being served.</summary>
    private async Task<Func<AuthorizationData>> BuildAsync(
        Func<CancellationToken, Task<Result<AuthorizationData>>> dataFactory,
        CacheEntry entry,
        CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.Now.UtcDateTime;
        var result = await dataFactory(cancellationToken);
        if (result is null || !result.IsSuccess)
        {
            throw new InvalidOperationException(result?.Errors.FirstOrDefault() ?? "Failed to get authorization data for user");
        }

        var subjectEntry = NeedsHydratedVersion(entry, result.Value)
            ? entry with { Versions = await accessVersionStore.ReadAsync(result.Value.TenantId, cancellationToken) } : entry;
        var project = await actions.EvaluateAsync(result.Value, subjectEntry, cancellationToken);
        return () => Stamp(project(dateTimeProvider.Now.UtcDateTime), now, entry.Lifetime);
    }

    private static AuthorizationData Stamp(AuthorizationData data, DateTime builtAt, TimeSpan lifetime)
    {
        var expiresAt = EarlierOf(builtAt.Add(lifetime), data.ExpiresAt);
        expiresAt = EarlierOf(expiresAt, RequiredActionEvaluation.NextTransition(data, builtAt));
        return data.WithCacheMetadata(builtAt, expiresAt, GenerateETag(data));
    }

    /// <summary>The entry's end, or the earliest permission expiry the data carries when that comes first.</summary>
    private static DateTime EarlierOf(DateTime entryEnd, DateTime permissionsExpireAt) =>
        permissionsExpireAt != default && permissionsExpireAt < entryEnd ? permissionsExpireAt : entryEnd;

    // External authorization strategies can map a positive token tenant to a different local tenant.
    private bool NeedsHydratedVersion(CacheEntry entry, AuthorizationData data) =>
        actions.HasProviders && entry.LookupTenant > 0 && entry.LookupTenant != data.TenantId;

    /// <summary>One cache entry's key, lifetime, tags and version scope.</summary>
    internal readonly record struct CacheEntry(string Key, TimeSpan Lifetime, string[] Tags, AccessVersionKey Versions, string Configuration, long LookupTenant)
    {
        /// <summary>The entry lives <see cref="Lifetime"/> in both cache levels.</summary>
        public HybridCacheEntryOptions Options => new() { Expiration = Lifetime, LocalCacheExpiration = Lifetime };
    }

    /// <inheritdoc />
    public async Task InvalidateAsync(string userId, long tenantId, CancellationToken cancellationToken)
    {
        await hybridCache.RemoveByTagAsync(GetUserAuthDataTag(userId), cancellationToken);
        await hybridCache.RemoveByTagAsync(GetUserTenantAuthDataTag(userId, tenantId), cancellationToken);
    }

    /// <inheritdoc />
    public async Task InvalidateAllAsync(CancellationToken cancellationToken)
    {
        await hybridCache.RemoveByTagAsync(AllAuthDataCacheTags, cancellationToken);
    }

    /// <summary>Per-token key; the access versions in it retire every entry when an authorization input changes.</summary>
    private string GetClaimsCacheKey(AccessVersionKey versions, string tokenPart) =>
        $"{CacheNamespace(versions, JWT_AUTH_DATA_CACHE_KEY_PREFIX)}:{tokenPart}";

    /// <summary>Per-user-and-tenant key; the access versions in it retire every entry when an authorization input changes.</summary>
    private string GetUserAuthDataCacheKey(AccessVersionKey versions, string userId, long tenantId) =>
        $"{CacheNamespace(versions, USER_AUTH_DATA_CACHE_KEY_PREFIX)}:{userId}:{tenantId}";

    private string CacheNamespace(AccessVersionKey versions, string kind) =>
        $"{versions.Prefix}:{kind}:{versions.Versions}.{actions.ConfigurationKey}";

    private static string GetUserAuthDataTag(string userId) => $"auth_data:user:{userId}";

    private static string GetUserTenantAuthDataTag(string userId, long tenantId) => $"auth_data:user:{userId}:tenant:{tenantId}";

    // Cache tag for all authorization data
    private static string[] AllAuthDataCacheTags => [ALL_AUTH_DATA_TAG];

    private static string[] GetAuthDataCacheTags(string userId, long tenantId) =>
    [
        ALL_AUTH_DATA_TAG,
        GetUserAuthDataTag(userId),
        GetUserTenantAuthDataTag(userId, tenantId)
    ];

    /// <summary>
    /// How long an entry lives: 60 seconds, or less when the principal's token expires sooner. The entry ends 10 seconds
    /// before the token; a token already inside that buffer gets a one-second entry, never a full one.
    /// </summary>
    private TimeSpan ComputeLifetime(ClaimsPrincipal? principal)
    {
        var expClaim = principal?.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
        if (expClaim is null || !long.TryParse(expClaim, out var expirySeconds))
        {
            return _maxEntryLifetime;
        }

        var timeUntilExpiry = DateTimeOffset.FromUnixTimeSeconds(expirySeconds) - dateTimeProvider.Now - _cacheExpiryBuffer;
        return TimeSpan.FromTicks(Math.Clamp(timeUntilExpiry.Ticks, _minEntryLifetime.Ticks, _maxEntryLifetime.Ticks));
    }

    /// <summary>
    /// Generates an ETag for authorization data based on its content.
    /// Centralized ETag generation ensures consistency across all strategies.
    /// </summary>
    private static string GenerateETag(AuthorizationData data)
    {
        var sortedRoles = string.Join(",", data.Roles.OrderBy(r => r).ToArray());
        var sortedPermissions = string.Join(",", data.Permissions.OrderBy(p => p).ToArray());
        var content = $"{data.UserId}:{data.TenantId}:{sortedRoles}:{sortedPermissions}";
        if (data.RequiredActionsUnavailable)
        {
            content += ":standing-unavailable";
        }
        if (data.RequiredActions.Count > 0)
        {
            content += ":" + JsonSerializer.Serialize(data.RequiredActions);
        }
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));

        var shortETag = Convert.ToBase64String(hash)[..ETAG_LENGTH];
        return shortETag;
    }
}
