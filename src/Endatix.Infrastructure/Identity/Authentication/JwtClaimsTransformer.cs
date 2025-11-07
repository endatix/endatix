using System.Security.Claims;
using Endatix.Core.Abstractions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Transforms JWT claims by enriching them with user permissions and roles from the database.
/// This enables FastEndpoints' built-in authorization to work with our RBAC system.
/// </summary>
public sealed class JwtClaimsTransformer(
    IPermissionService permissionService,
    IDateTimeProvider dateTimeProvider,
    HybridCache hybridCache) : IClaimsTransformation
{
    private static readonly TimeSpan _cacheExpiryBuffer = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _fallbackCacheExpiration = TimeSpan.FromMinutes(15);

    public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
    {
        if (principal.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return principal;
        }

        var userId = principal.GetUserId();
        if (userId is null)
        {
            return principal;
        }

        if (!long.TryParse(userId, out var endatixUserId))
        {
            return principal;
        }

        await HydrateClaimsWithAuthorization(identity, endatixUserId);

        return principal;
    }


    /// <summary>
    /// Hydrates the claims identity with the user's roles and permissions from the database.
    /// </summary>
    /// <param name="identity">The claims identity to hydrate.</param>
    /// <param name="userId">The user ID to hydrate the claims for.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    private async Task HydrateClaimsWithAuthorization(ClaimsIdentity identity, long userId, CancellationToken cancellationToken = default)
    {
        var claimIdentityId = identity.FindFirst(JwtRegisteredClaimNames.Jti)?.Value ?? $"jti_{userId}";
        var cacheExpiration = ComputeCacheExpiration(identity);
        var cacheKey = $"usr_cache:{claimIdentityId}";

        var claimsData = await hybridCache.GetOrCreateAsync(
            cacheKey,
            async _ => await GetUserClaimsDataAsync(userId, cancellationToken),
            new HybridCacheEntryOptions
            {
                Expiration = cacheExpiration,
                LocalCacheExpiration = cacheExpiration
            },
            tags: ["claims_hydrated"],
            cancellationToken
        );

        if (claimsData is null)
        {
            return;
        }

        foreach (var role in claimsData.Roles)
        {
            identity.AddClaim(new Claim(ClaimTypes.Role, role));
        }

        foreach (var permission in claimsData.Permissions)
        {
            identity.AddClaim(new Claim(ClaimNames.Permission, permission));
        }

        if (claimsData.IsAdmin)
        {
            identity.AddClaim(new Claim(ClaimNames.IsAdmin, "true"));
        }

        identity.AddClaim(new Claim(ClaimNames.Hydrated, "true"));
    }

    /// <summary>
    /// Gets the user claims data from the database.
    /// </summary>
    /// <param name="userId">The user ID to get the claims data for.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    /// <returns>The user claims data.</returns>
    private async Task<UserClaimsData?> GetUserClaimsDataAsync(long userId, CancellationToken cancellationToken = default)
    {
        var userRoleInfoResult = await permissionService.GetUserPermissionsInfoAsync(userId, cancellationToken);
        if (!userRoleInfoResult.IsSuccess)
        {
            return null;
        }

        var roleInfo = userRoleInfoResult.Value;

        return new UserClaimsData
        {
            Roles = roleInfo.Roles,
            Permissions = roleInfo.Permissions,
            IsAdmin = roleInfo.IsAdmin
        };
    }

    /// <summary>
    /// Computes the cache expiration time for the claims principal authorization hydrated claims.
    /// </summary>
    /// <param name="identity">The claims identity to compute the cache expiration for.</param>
    /// <returns>The cache expiration time.</returns>
    private TimeSpan ComputeCacheExpiration(ClaimsIdentity identity)
    {

        var claimExpiry = identity.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
        if (claimExpiry != null && long.TryParse(claimExpiry, out var expirySeconds))
        {
            var expirationDateTime = DateTimeOffset.FromUnixTimeSeconds(expirySeconds);
            var timeUntilExpiry = expirationDateTime - dateTimeProvider.Now - _cacheExpiryBuffer;
            return timeUntilExpiry > TimeSpan.Zero ? timeUntilExpiry : _fallbackCacheExpiration;
        }

        return _fallbackCacheExpiration;
    }

    private sealed record UserClaimsData
    {
        public IEnumerable<string> Roles { get; init; } = Enumerable.Empty<string>();
        public IEnumerable<string> Permissions { get; init; } = Enumerable.Empty<string>();
        public bool IsAdmin { get; init; }
    }
}