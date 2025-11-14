using System.ComponentModel.DataAnnotations;

namespace Endatix.Infrastructure.Identity.Authentication.Providers;

/// <summary>
/// Configuration options for Endatix JWT authentication provider
/// </summary>
public class EndatixJwtOptions : JwtAuthProviderOptions
{
    private const string DEFAULT_ISSUER = "endatix-api";

    /// <summary>
    /// The key used to sign the JWT token
    /// </summary>
    [Required]
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>
    /// The expiration time of the JWT access token in minutes
    /// </summary>
    public int AccessExpiryInMinutes { get; set; } = 15;

    /// <summary>
    /// The expiration time of the JWT refresh token in days
    /// </summary>
    public int RefreshExpiryInDays { get; set; } = 7;

    /// <summary>
    /// Valid issuer for the JWT token - overrides base class to provide proper default
    /// </summary>
    [Required]
    public new string Issuer { get; set; } = DEFAULT_ISSUER;

    public EndatixJwtOptions()
    {
        SchemeName = AuthSchemes.EndatixJwt;
        Audiences = ["endatix-hub"];
    }
}