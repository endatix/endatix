using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Caching;
using Endatix.Infrastructure.Identity;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Endatix.Infrastructure.Identity.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Internal helper class for managing the authorization cache.
/// </summary>
/// <remarks>
/// Every key carries this deployment's prefix and the access versions of the request's tenant and of system roles,
/// read uncached on each lookup. A role change writes a new access version for its tenant as the last statement of its
/// transaction, so every API instance misses its old entries on the next request; <see cref="InvalidateAsync"/> only
/// frees memory on the instance it runs on. An entry lives at most 60 seconds, never past its
/// token, and is never served past the earliest expiry of the role permissions it holds.
/// </remarks>
internal class AuthorizationCache(
    HybridCache hybridCache,
    IDateTimeProvider dateTimeProvider,
    IHttpContextAccessor httpContextAccessor,
    IOptions<EndatixJwtOptions> endatixJwtOptions,
    IAccessVersionStore accessVersionStore) : IAuthorizationCache
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

    /// <inheritdoc />
    public async Task<AuthorizationData> GetOrCreateAsync(ClaimsPrincipal principal, Func<CancellationToken, Task<Result<AuthorizationData>>> dataFactory, CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId() ?? throw new InvalidOperationException("Principal must have a user ID");
        var tenantId = principal.GetTenantId();
        var accessToken = BearerAccessTokenResolver.Resolve(httpContextAccessor);
        var claimIdentityId = !string.IsNullOrWhiteSpace(accessToken)
            ? accessToken
            : principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value ?? $"jti_{userId}_{tenantId}";
        var cacheKeyTokenPart = CacheKeyFingerprint.ComputeHmacSha256Hex(
            claimIdentityId,
            endatixJwtOptions.Value.SigningKey);
        var versions = await accessVersionStore.ReadAsync(tenantId, cancellationToken);

        return await GetOrCreateEntryAsync(
            GetClaimsCacheKey(versions, cacheKeyTokenPart),
            ComputeLifetime(principal),
            GetAuthDataCacheTags(userId, tenantId),
            dataFactory,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AuthorizationData> GetOrCreateAsync(string userId, long tenantId, ClaimsPrincipal? principal, Func<CancellationToken, Task<Result<AuthorizationData>>> dataFactory, CancellationToken cancellationToken)
    {
        var versions = await accessVersionStore.ReadAsync(tenantId, cancellationToken);

        return await GetOrCreateEntryAsync(
            GetUserAuthDataCacheKey(versions, userId, tenantId),
            ComputeLifetime(principal),
            GetAuthDataCacheTags(userId, tenantId),
            dataFactory,
            cancellationToken);
    }

    /// <summary>
    /// Serves the entry under <paramref name="cacheKey"/>, building it on a miss. An entry past its own
    /// <see cref="AuthorizationData.ExpiresAt"/> (a role permission it holds has lapsed) counts as a miss: HybridCache
    /// fixes an entry's lifetime before the data is built, so the data carries the shorter bound itself.
    /// </summary>
    private async Task<AuthorizationData> GetOrCreateEntryAsync(
        string cacheKey,
        TimeSpan lifetime,
        string[] tags,
        Func<CancellationToken, Task<Result<AuthorizationData>>> dataFactory,
        CancellationToken cancellationToken)
    {
        var entryOptions = new HybridCacheEntryOptions
        {
            Expiration = lifetime,
            LocalCacheExpiration = lifetime
        };

        var cached = await hybridCache.GetOrCreateAsync(
            cacheKey,
            async _ => await BuildAsync(dataFactory, lifetime, cancellationToken),
            entryOptions,
            tags,
            cancellationToken);
        if (cached.ExpiresAt > dateTimeProvider.Now.UtcDateTime)
        {
            return cached;
        }

        var rebuilt = await BuildAsync(dataFactory, lifetime, cancellationToken);
        await hybridCache.SetAsync(cacheKey, rebuilt, entryOptions, tags, cancellationToken);
        return rebuilt;
    }

    /// <summary>Builds the data and stamps it with the time it stops being served.</summary>
    private async Task<AuthorizationData> BuildAsync(
        Func<CancellationToken, Task<Result<AuthorizationData>>> dataFactory,
        TimeSpan lifetime,
        CancellationToken cancellationToken)
    {
        var now = dateTimeProvider.Now.UtcDateTime;
        var result = await dataFactory(cancellationToken);
        if (result is null || !result.IsSuccess)
        {
            throw new InvalidOperationException(result?.Errors.FirstOrDefault() ?? "Failed to get authorization data for user");
        }

        var expiresAt = now.Add(lifetime);
        var permissionsExpireAt = result.Value.ExpiresAt;
        if (permissionsExpireAt != default && permissionsExpireAt < expiresAt)
        {
            expiresAt = permissionsExpireAt;
        }

        return result.Value.WithCacheMetadata(now, expiresAt, GenerateETag(result.Value));
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
    private static string GetClaimsCacheKey(AccessVersionKey versions, string tokenPart) =>
        $"{versions.Prefix}:{JWT_AUTH_DATA_CACHE_KEY_PREFIX}:{versions.Versions}:{tokenPart}";

    /// <summary>Per-user-and-tenant key; the access versions in it retire every entry when an authorization input changes.</summary>
    private static string GetUserAuthDataCacheKey(AccessVersionKey versions, string userId, long tenantId) =>
        $"{versions.Prefix}:{USER_AUTH_DATA_CACHE_KEY_PREFIX}:{versions.Versions}:{userId}:{tenantId}";

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
    /// How long an entry lives: 60 seconds, or less when the principal's token expires sooner
    /// (with a buffer, so an entry never outlives its token).
    /// </summary>
    private TimeSpan ComputeLifetime(ClaimsPrincipal? principal)
    {
        var expClaim = principal?.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
        if (expClaim is not null && long.TryParse(expClaim, out var expirySeconds))
        {
            var timeUntilExpiry = DateTimeOffset.FromUnixTimeSeconds(expirySeconds) - dateTimeProvider.Now - _cacheExpiryBuffer;
            if (timeUntilExpiry > TimeSpan.Zero && timeUntilExpiry < _maxEntryLifetime)
            {
                return timeUntilExpiry;
            }
        }

        return _maxEntryLifetime;
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
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(content));

        var shortETag = Convert.ToBase64String(hash)[..ETAG_LENGTH];
        return shortETag;
    }
}
