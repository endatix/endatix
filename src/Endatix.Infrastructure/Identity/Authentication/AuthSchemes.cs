namespace Endatix.Infrastructure.Identity.Authentication;

public static class AuthSchemes
{
    public const string EndatixJwt = "EndatixJwt";

    /// <summary>JWT bearer for ReBAC / form-access tokens (distinct issuer and audience from hub tokens).</summary>
    public const string EndatixReBac = "EndatixReBac";
}