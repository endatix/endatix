using System.ComponentModel.DataAnnotations;
using Endatix.Infrastructure.Identity.Authentication;

namespace Endatix.Infrastructure.Identity.Providers;

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


    /// <summary>
    /// Keycloak introspection endpoint
    /// </summary>
    public string IntrospectionEndpoint => $"{Issuer}/protocol/openid-connect/token/introspect";

    /// <summary>
    /// Keycloak client secret
    /// </summary>
    [Required]
    public string ClientSecret { get; set; } = string.Empty;

    /// <summary>
    /// Keycloak client ID
    /// </summary>
    [Required]
    public string ClientId { get; set; } = string.Empty;

    /// <summary>
    /// Keycloak authorization strategy options
    /// </summary>
    public KeycloakAuthorizationStrategyOptions Authorization { get; set; } = new();
}