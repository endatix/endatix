using System.ComponentModel.DataAnnotations;

namespace Endatix.Infrastructure.Identity.Providers;

/// <summary>
/// Options for the Keycloak authorization strategy.
/// </summary>
public class KeycloakAuthorizationStrategyOptions : AuthorizationStrategyOptionsBase
{
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