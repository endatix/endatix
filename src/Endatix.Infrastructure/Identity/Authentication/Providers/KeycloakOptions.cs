using System.ComponentModel.DataAnnotations;

namespace Endatix.Infrastructure.Identity.Authentication.Providers;

public class KeycloakOptions : JwtAuthProviderOptions
{
    /// <summary>
    /// The configuration section name where these options are stored
    /// </summary>
    public const string SECTION_NAME = "Endatix:Auth:Providers:Keycloak";

    /// <summary>
    /// Keycloak realm URL
    /// </summary>
    [Required]
    [Url]
    public string RealmUrl { get; set; } = string.Empty;

    /// <summary>
    /// Keycloak audience
    /// </summary>
    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// OpenID Connect metadata address
    /// </summary>
    public string MetadataAddress => $"{RealmUrl}/.well-known/openid-configuration";

    public KeycloakOptions()
    {
        SchemeName = AuthSchemes.Keycloak;
        ValidIssuer = RealmUrl;
        ValidateIssuer = false;
        ValidateAudience = false;
        ValidateLifetime = false;
        ValidateIssuerSigningKey = false;
    }
}