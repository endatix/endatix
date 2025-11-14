using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity;
using Microsoft.Extensions.Caching.Hybrid;


/// <summary>
/// Internal helper class for managing the authorization cache.
/// </summary>
internal class AuthorizationCache(
    HybridCache hybridCache,
    IDateTimeProvider dateTimeProvider) : IAuthorizationCache
{
    private const short ETAG_LENGTH = 12;

    // Cache key prefixes
    private const string JWT_AUTH_DATA_CACHE_KEY_PREFIX = "jwt_auth";
    private const string USER_AUTH_DATA_CACHE_KEY_PREFIX = "usr_auth";

    // Cache tags
    private const string ALL_AUTH_DATA_TAG = "auth_data:all";

    // Default expiration
    private static readonly TimeSpan _defaultExpiration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan _cacheExpiryBuffer = TimeSpan.FromSeconds(10);

    /// <inheritdoc />
    public async Task<AuthorizationData> GetOrCreateAsync(ClaimsPrincipal principal, Func<CancellationToken, Task<Result<AuthorizationData>>> dataFactory, CancellationToken cancellationToken)
    {
        var userId = principal.GetUserId();
        if (userId is null)
        {
            throw new InvalidOperationException("Principal must have a user ID");
        }

        var cacheExpiration = ComputeExpiration(principal);
        var claimIdentityId = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value ?? $"jti_{userId}";
        var cacheKey = GetClaimsCacheKey(claimIdentityId);
        var now = dateTimeProvider.Now.UtcDateTime;

        return await hybridCache.GetOrCreateAsync(
            cacheKey,
            async _ =>
            {
                var result = await dataFactory(cancellationToken);
                if (result is null || !result.IsSuccess)
                {
                    throw new InvalidOperationException(result?.Errors.FirstOrDefault() ?? "Failed to get authorization data for user");
                }

                var etag = GenerateETag(result.Value);
                return result.Value.WithCacheMetadata(now, now.Add(cacheExpiration), etag);
            },
            new HybridCacheEntryOptions
            {
                Expiration = cacheExpiration,
                LocalCacheExpiration = cacheExpiration
            },
            tags: AllAuthDataCacheTags,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<AuthorizationData> GetOrCreateAsync(string userId, long tenantId, ClaimsPrincipal? principal, Func<CancellationToken, Task<Result<AuthorizationData>>> dataFactory, CancellationToken cancellationToken)
    {
        var cacheKey = GetUserAuthDataCacheKey(userId, tenantId);
        var expiration = principal is not null
            ? ComputeExpiration(principal)
            : _defaultExpiration;
        var tags = AllAuthDataCacheTags;
        var now = dateTimeProvider.Now.UtcDateTime;

        return await hybridCache.GetOrCreateAsync(
            cacheKey,
            async _ =>
            {
                var result = await dataFactory(cancellationToken);
                if (result is null || !result.IsSuccess)
                {
                    throw new InvalidOperationException(result?.Errors.FirstOrDefault() ?? "Failed to get authorization data for user");
                }

                var etag = GenerateETag(result.Value);
                return result.Value.WithCacheMetadata(now, now.Add(expiration), etag);
            },
            new HybridCacheEntryOptions
            {
                Expiration = expiration,
                LocalCacheExpiration = expiration
            },
            tags: tags,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task InvalidateAsync(string userId, long tenantId, CancellationToken cancellationToken = default)
    {
        var cacheKey = GetUserAuthDataCacheKey(userId, tenantId);
        await hybridCache.RemoveAsync(cacheKey, cancellationToken);
    }

    /// <inheritdoc />
    public async Task InvalidateAllAsync(CancellationToken cancellationToken = default)
    {
        await hybridCache.RemoveByTagAsync(AllAuthDataCacheTags, cancellationToken);
    }

    // Generate cache key for saving authorization data for a claims principal given the JWT Identity ID (jti)
    private static string GetClaimsCacheKey(string jti) => $"{JWT_AUTH_DATA_CACHE_KEY_PREFIX}:{jti}";

    // Generate cache key for saving authorization data for a user with explicit userId and tenantId
    private static string GetUserAuthDataCacheKey(string userId, long tenantId) => $"{USER_AUTH_DATA_CACHE_KEY_PREFIX}:{userId}:{tenantId}";

    // Cache tag for all authorization data
    private static string[] AllAuthDataCacheTags => [ALL_AUTH_DATA_TAG];

    /// <summary>
    /// Computes the expiration time for the authorization data cache.
    /// </summary>
    /// <param name="principal">The claims principal to compute the expiration for.</param>
    /// <param name="fallback">The fallback expiration time if the principal is null.</param>
    /// <returns>The expiration time.</returns>
    private TimeSpan ComputeExpiration(ClaimsPrincipal? principal, TimeSpan? fallback = null)
    {
        if (principal is null)
        {
            return fallback ?? _defaultExpiration;
        }


        var expClaim = principal.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
        if (expClaim is not null && long.TryParse(expClaim, out var expirySeconds))
        {
            var expirationDateTime = DateTimeOffset.FromUnixTimeSeconds(expirySeconds);
            var timeUntilExpiry = expirationDateTime - DateTimeOffset.UtcNow - _cacheExpiryBuffer;
            return timeUntilExpiry > TimeSpan.Zero ? timeUntilExpiry : (fallback ?? _defaultExpiration);
        }

        return fallback ?? _defaultExpiration;
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