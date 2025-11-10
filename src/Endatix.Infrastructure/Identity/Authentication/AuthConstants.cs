namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Constants for authentication
/// </summary>

public static class AuthConstants
{
    /// <summary>
    /// Constants for authentication schemes
    /// </summary>
    public static class AuthSchemes
    {
        public const string EndatixJwt = "EndatixJwt";
    }

    /// <summary>
    /// The default tenant ID to use for authentication if not specified in the claims
    /// </summary>
    public const long DEFAULT_TENANT_ID = 0;

    /// <summary>
    /// The default tenant ID to use for the admin user
    /// </summary>
    public const long DEFAULT_ADMIN_TENANT_ID = 1;
}