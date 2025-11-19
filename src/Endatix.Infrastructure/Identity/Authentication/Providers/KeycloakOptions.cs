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
    public KeycloakAuthorizationStrategyOptions? Authorization { get; set; }


    /// <summary>
    /// Options for the Keycloak authorization strategy.
    /// </summary>
    public class KeycloakAuthorizationStrategyOptions
    {
        /// <summary>
        /// The role mappings for the authorization strategy.
        /// </summary>
        public Dictionary<string, string> RoleMappings { get; set; } = new();

        /// <summary>
        /// Dot notation path to the roles in the introspection response. For example: "resource_access.endatix.roles".
        /// </summary>
        [Required]
        public string RolesPath { get; set; } = "resource_access.{ClientId}.roles";

        /// <summary>
        /// Resolve the roles path with the client ID. For example: "resource_access.endatix.roles" -> "resource_access.endatix-hub.roles".
        /// </summary>
        public string ResolveRolesPath(string clientId) => RolesPath.Replace("{ClientId}", clientId);
    }
}