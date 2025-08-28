using System.ComponentModel.DataAnnotations;
using Endatix.Infrastructure.Identity.Authentication;


/// <summary>
/// Configuration options for Endatix JWT authentication provider
/// </summary>
public class EndatixJwtOptions : JwtAuthProviderOptions
{
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


    public EndatixJwtOptions()
    {
        SchemeName = AuthSchemes.EndatixJwt;
        ValidIssuer = "endatix-api";
        ValidAudiences = ["endatix-hub"];
    }
}