namespace Endatix.Infrastructure.Identity;

/// <summary>
/// Defines custom claim types used in the Endatix application.
/// </summary>
public static class ClaimNames
{
    /// <summary>
    /// Represents a claim for user permissions.
    /// </summary>
    public const string Permission = "permission";

    /// <summary>
    /// Represents a claim indicating whether the user's email is verified.
    /// </summary>
    public const string EmailVerified = "email_verified";
}