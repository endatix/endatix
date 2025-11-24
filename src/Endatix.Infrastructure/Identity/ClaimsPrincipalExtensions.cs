using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Authentication;

namespace Endatix.Infrastructure.Identity;


/// <summary>
/// Extension methods for <see cref="ClaimsPrincipal"/> to extract user and tenant information.
/// These methods provide a consistent way to extract identity data from claims across the application.
/// </summary>
public static class ClaimsPrincipalExtensions
{
    /// <summary>
    /// Checks if the claims principal has been hydrated with RBAC information from the database or remote data source.
    /// </summary>
    /// <param name="principal">The claims principal to check.</param>
    /// <returns>True if the claims principal has been hydrated with RBAC information, false otherwise.</returns>
    public static bool IsHydrated(this ClaimsPrincipal principal)
    {
        if (principal?.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return false;
        }

        var endatixIdentity = principal.Identities
            .OfType<AuthorizedIdentity>()
            .FirstOrDefault();

        if (endatixIdentity is not null)
        {
            return endatixIdentity.IsHydrated;
        }

        return identity.HasClaim(c => c.Type == ClaimNames.Hydrated && c.Value == "true");
    }

    /// <summary>
    /// Gets the user ID from the claims principal.
    /// </summary>
    /// <param name="principal">The claims principal to get the user ID from.</param>
    /// <returns>The user ID as a string, or null if not authenticated or the user ID claim is not found.</returns>
    public static string? GetUserId(this ClaimsPrincipal principal)
    {
        if (principal?.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return null;
        }

        var userIdClaim = identity.FindFirst(ClaimNames.UserId) ??
                         identity.FindFirst(ClaimTypes.NameIdentifier);

        if (userIdClaim is null)
        {
            return null;
        }

        return userIdClaim.Value;
    }

    /// <summary>
    /// Gets the tenant ID from the claims principal.
    /// </summary>
    /// <param name="principal">The claims principal to get the tenant ID from.</param>
    /// <returns>The tenant ID as a long, or the default tenant ID if not authenticated or the tenant ID claim is not found.</returns>
    public static long GetTenantId(this ClaimsPrincipal principal)
    {
        if (principal?.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return AuthConstants.DEFAULT_TENANT_ID;
        }

        var endatixIdentity = principal.Identities
            .OfType<AuthorizedIdentity>()
            .FirstOrDefault();

        if (endatixIdentity is not null)
        {
            return endatixIdentity.TenantId;
        }

        return AuthConstants.DEFAULT_TENANT_ID;
    }


    /// <summary>
    /// Gets the issuer from the claims principal.
    /// </summary>
    /// <param name="principal">The claims principal to get the issuer from.</param>
    /// <returns>The issuer as a string, or null if not authenticated or the issuer claim is not found.</returns>
    public static string? GetIssuer(this ClaimsPrincipal principal)
    {
        if (principal?.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return null;
        }

        return identity.FindFirst(JwtRegisteredClaimNames.Iss)?.Value;
    }

    /// <summary>
    /// Checks if the claims principal is an admin.
    /// </summary>
    /// <param name="principal">The claims principal to check.</param>
    /// <returns>True if the claims principal is an admin, false otherwise. Returns an error result if the claims principal is not hydrated, in which case, the caller mut check with the Authorization Info Source directly.</returns>
    public static Result<bool> IsAdmin(this ClaimsPrincipal principal)
    {
        if (principal.IsHydrated())
        {
            var isAdmin = principal.HasClaim(ClaimNames.IsAdmin, "true");
            return Result.Success(isAdmin);
        }

        return Result.Error("Claims principal is not hydrated");
    }
}