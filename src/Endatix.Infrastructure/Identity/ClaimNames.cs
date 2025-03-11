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
    /// Represents a claim indicating whether the user's email is verified.
    /// </summary>
    public const string EmailVerified = "email_verified";

    /// <summary>
    /// Represents a claim for the user's tenant identifier.
    /// </summary>
    public const string TenantId = "tenant_id";
}
