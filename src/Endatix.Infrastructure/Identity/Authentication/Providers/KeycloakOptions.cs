using System.ComponentModel.DataAnnotations;

namespace Endatix.Infrastructure.Identity.Authentication.Providers;

public class KeycloakOptions : JwtAuthProviderOptions
{
    /// <summary>
    /// Keycloak audience
    /// </summary>
    [Required]
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// OpenID Connect metadata address
    /// </summary>
    public string MetadataAddress => $"{Issuer}/.well-known/openid-configuration";
}