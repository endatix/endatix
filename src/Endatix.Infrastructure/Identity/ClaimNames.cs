using System.IdentityModel.Tokens.Jwt;

namespace Endatix.Infrastructure.Identity;

/// <summary>
/// Defines custom claim types used in the Endatix application.
/// </summary>
public static class ClaimNames
{
    /// <summary>
    /// Represents a claim for user roles.
    /// </summary>
    public const string Role = "role";

    /// <summary>
    /// Represents a claim for user permissions.
    /// </summary>
    public const string Permission = "permission";

    /// <summary>
    /// Represents a claim for the user's name.
    /// </summary>
    public const string Email = JwtRegisteredClaimNames.Email;

    /// <summary>
    /// Represents a claim indicating whether the user's email is verified.
    /// </summary>
    public const string EmailVerified = JwtRegisteredClaimNames.EmailVerified;

    /// <summary>
    /// Represents a claim for the user's tenant identifier.
    /// </summary>
    public const string TenantId = "tid";

    /// <summary>
    /// Represents a claim for the user's unique identifier.
    /// Uses standard JWT 'sub' (subject) claim for better interoperability.
    /// </summary>
    public const string UserId = JwtRegisteredClaimNames.Sub;

    /// <summary>
    /// Represents a claim indicating whether the user is an administrator.
    /// When true, the user bypasses all permission checks for performance.
    /// </summary>
    public const string IsAdmin = "is_admin";

    /// <summary>
    /// Represents a claim indicating whether the claims principal has been hydrated with RBAC information from the database or remote data source.
    /// Used for idempotency checks to prevent multiple transformations of the same claims principal.
    /// </summary>
    public const string Hydrated = "hydrated";


    /// <summary>
    /// Represents a claim for the cached at timestamp.
    /// </summary>
    public const string CachedAt = "cached_at";

    /// <summary>
    /// Represents a claim for the expires at timestamp.
    /// </summary>
    public const string ExpiresAt = "expires_at";

    /// <summary>
    /// Represents a claim for the ETag.
    /// </summary>
    public const string ETag = "etag";
}
