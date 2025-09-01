using System.ComponentModel.DataAnnotations;

namespace Endatix.Infrastructure.Identity.Authentication.Providers;

public class GoogleOptions : JwtAuthProviderOptions
{
    /// <summary>
    /// The configuration section name where these options are stored
    /// </summary>
    public const string SECTION_NAME = "Endatix:Auth:Providers:Google";

    /// <summary>
    /// Google realm URL
    /// </summary>
    [Required]
    [Url]
    public string RealmUrl { get; set; } = "https://accounts.google.com";

    /// <summary>
    /// Google audience
    /// </summary>
    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// OpenID Connect metadata address
    /// </summary>
    public string MetadataAddress => $"{RealmUrl}/.well-known/openid-configuration";

    public GoogleOptions()
    {
        SchemeName = GoogleAuthProvider.GOOGLE_ID;
        ValidIssuer = RealmUrl;
        ValidateIssuer = true;
        ValidateAudience = true;
        ValidateLifetime = true;
        ValidateIssuerSigningKey = true;
    }
}