using System.Security.Claims;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Infrastructure.Identity.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.IdentityModel.JsonWebTokens;

namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Transforms claims principal by enriching them with user permissions and roles from the database.
/// This enables FastEndpoints' built-in authorization to work with our RBAC system.
/// </summary>
internal sealed class ClaimsTransformer(
    IEnumerable<IAuthorizationStrategy> authorizationStrategies,
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

        var authorizationData = await GetAuthorizationDataAsync(principal);
        if (authorizationData is not null)
        {
            principal.AddIdentity(new AuthorizedIdentity(authorizationData));
        }

        return principal;
    }


    /// <summary>
    /// Gets the authorization data for the claims principal.
    /// </summary>
    /// <param name="principal">The claims principal.</param>
    /// <param name="cancellationToken">The cancellation token to cancel the operation.</param>
    private async Task<AuthorizationData?> GetAuthorizationDataAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        var userId = principal.GetUserId();
        if (userId is null)
        {
            return null;
        }

        var claimIdentityId = principal.FindFirst(JwtRegisteredClaimNames.Jti)?.Value ?? $"jti_{userId}";
        var cacheExpiration = ComputeCacheExpiration(principal);
        var cacheKey = $"usr_cache:{claimIdentityId}";

        var authorizationProvider = GetAuthorizationStrategy(principal);
        if (authorizationProvider is null)
        {
            return null;
        }

        var authorizationData = await hybridCache.GetOrCreateAsync(
            cacheKey,
            async _ => await authorizationProvider.GetAuthorizationDataAsync(principal, cancellationToken),
            new HybridCacheEntryOptions
            {
                Expiration = cacheExpiration,
                LocalCacheExpiration = cacheExpiration
            },
            tags: ["auth_data"],
            cancellationToken
        );

        return authorizationData;
    }

    private IAuthorizationStrategy? GetAuthorizationStrategy(ClaimsPrincipal principal)
    {
        var issuer = principal.GetIssuer();
        if (issuer is null)
        {
            return null;
        }

        return authorizationStrategies.FirstOrDefault(provider => provider.CanHandle(principal));
    }

    /// <summary>
    /// Computes the cache expiration time for the claims principal authorization hydrated claims.
    /// </summary>
    /// <param name="principal">The claims identity to compute the cache expiration for.</param>
    /// <returns>The cache expiration time.</returns>
    private TimeSpan ComputeCacheExpiration(ClaimsPrincipal principal)
    {

        var claimExpiry = principal.FindFirst(JwtRegisteredClaimNames.Exp)?.Value;
        if (claimExpiry != null && long.TryParse(claimExpiry, out var expirySeconds))
        {
            var expirationDateTime = DateTimeOffset.FromUnixTimeSeconds(expirySeconds);
            var timeUntilExpiry = expirationDateTime - dateTimeProvider.Now - _cacheExpiryBuffer;
            return timeUntilExpiry > TimeSpan.Zero ? timeUntilExpiry : _fallbackCacheExpiration;
        }

        return _fallbackCacheExpiration;
    }
}