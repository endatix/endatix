using System.Security.Claims;

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
    /// <returns>The tenant ID as a string, or null if not authenticated or the tenant ID claim is not found.</returns>
    public static string? GetTenantId(this ClaimsPrincipal principal)
    {
        if (principal?.Identity is not ClaimsIdentity identity || !identity.IsAuthenticated)
        {
            return null;
        }

        return identity.FindFirst(ClaimNames.TenantId)?.Value;
    }
}