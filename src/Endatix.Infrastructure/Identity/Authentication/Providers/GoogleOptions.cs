using System.ComponentModel.DataAnnotations;

namespace Endatix.Infrastructure.Identity.Authentication.Providers;
public class GoogleOptions : JwtAuthProviderOptions
{
    private const string DEFAULT_ISSUER = "https://accounts.google.com";

    /// <summary>
    /// The configuration section name where these options are stored
    /// </summary>
    public const string SECTION_NAME = "Endatix:Auth:Providers:Google";

    public GoogleOptions()
    {
        SchemeName = GoogleAuthProvider.GOOGLE_SCHEME_NAME;
        Issuer = DEFAULT_ISSUER;
        ValidateIssuer = true;
        ValidateAudience = true;
        ValidateLifetime = true;
        ValidateIssuerSigningKey = true;
    }

    /// <summary>
    /// Google audience
    /// </summary>
    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// OpenID Connect metadata address
    /// </summary>
    public string MetadataAddress => $"{Issuer}/.well-known/openid-configuration";
}