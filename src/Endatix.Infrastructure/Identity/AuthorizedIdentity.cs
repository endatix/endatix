using System.Globalization;
using System.Security.Claims;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Infrastructure.Identity.Authentication;

namespace Endatix.Infrastructure.Identity;

/// <summary>
/// Represents an authorized identity with RBAC claims.
/// Has information about the user's roles, permissions, and tenant.
/// </summary>
public sealed class AuthorizedIdentity : ClaimsIdentity
{
    public const string AuthType = "Endatix";

    public AuthorizedIdentity(AuthorizationData authData)
            : base(BuildAuthorizationClaims(authData), AuthType)
    {
    }


    /// <summary>
    /// Checks if this identity has been hydrated with RBAC data.
    /// </summary>
    public bool IsHydrated => HasClaim(ClaimNames.Hydrated, "true");

    /// <summary>
    /// Gets the tenant ID from this identity.
    /// </summary>
    public long TenantId
    {
        get
        {
            var tenantIdClaim = FindFirst(ClaimNames.TenantId);
            if (tenantIdClaim is null || !long.TryParse(tenantIdClaim.Value, out var tenantId))
            {
                return AuthConstants.DEFAULT_TENANT_ID;
            }

            return tenantId;
        }
    }

    /// <summary>
    /// Gets all permission claims from this identity.
    /// </summary>
    public IEnumerable<string> Permissions =>
        FindAll(ClaimNames.Permission)
        .Select(c => c.Value)
        .Distinct();

    /// <summary>
    /// Gets all role claims from this identity.
    /// </summary>
    public IEnumerable<string> Roles =>
        FindAll(ClaimTypes.Role)
        .Select(c => c.Value)
        .Distinct();

    /// <summary>
    /// Checks if this identity represents an admin user.
    /// </summary>
    public bool IsAdmin => HasClaim(ClaimNames.IsAdmin, "true");


    /// <summary>
    /// Gets the cached at timestamp from this identity.
    /// </summary>
    public DateTime CachedAt => DateTime.Parse(FindFirst(ClaimNames.CachedAt)?.Value ?? DateTime.UtcNow.ToString("O"), null, DateTimeStyles.RoundtripKind);

    /// <summary>
    /// Gets the cache expires in from this identity.
    /// </summary>
    public DateTime CacheExpiresIn => DateTime.Parse(FindFirst(ClaimNames.ExpiresAt)?.Value ?? DateTime.UtcNow.ToString("O"), null, DateTimeStyles.RoundtripKind);

    /// <summary>
    /// Gets the ETag from this identity.
    /// </summary>
    public string ETag => FindFirst(ClaimNames.ETag)?.Value ?? string.Empty;

    /// <summary>
    /// Static factory method to build an authorized identity from an authorization data.
    /// </summary>
    /// <param name="authorizationData">The authorization data to build the identity from.</param>
    /// <returns>The authorized identity.</returns>
    private static IEnumerable<Claim> BuildAuthorizationClaims(AuthorizationData authorizationData)
    {
        var claims = new List<Claim>();

        if (authorizationData is null)
        {
            return claims;
        }

        claims.Add(new Claim(ClaimNames.TenantId, authorizationData.TenantId.ToString()));

        foreach (var role in authorizationData.Roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        foreach (var permission in authorizationData.Permissions)
        {
            claims.Add(new Claim(ClaimNames.Permission, permission));
        }

        if (authorizationData.IsAdmin)
        {
            claims.Add(new Claim(ClaimNames.IsAdmin, "true"));
        }

        claims.Add(new Claim(ClaimNames.CachedAt, authorizationData.CachedAt.ToString("O")));
        claims.Add(new Claim(ClaimNames.ExpiresAt, authorizationData.ExpiresAt.ToString("O")));
        claims.Add(new Claim(ClaimNames.ETag, authorizationData.ETag));

        claims.Add(new Claim(ClaimNames.Hydrated, "true"));

        return claims;
    }
}