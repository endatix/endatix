namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Result of introspecting a Keycloak token.
/// </summary>
/// <param name="ExternalRoles">The external roles.</param>
public sealed record KeycloakTokenIntrospectionResult(string[] ExternalRoles);
