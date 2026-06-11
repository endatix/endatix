using Endatix.Infrastructure.Identity.Provisioning;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Result of introspecting a Keycloak token.
/// </summary>
/// <param name="ExternalRoles">The external roles.</param>
/// <param name="Profile">The profile claims returned by token introspection.</param>
public sealed record KeycloakTokenIntrospectionResult(
    string[] ExternalRoles,
    ExternalIdentityProfile Profile);
