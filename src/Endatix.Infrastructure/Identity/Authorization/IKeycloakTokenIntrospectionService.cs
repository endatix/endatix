using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Authentication.Providers;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Service for introspecting Keycloak tokens.
/// </summary>
public interface IKeycloakTokenIntrospectionService
{
    /// <summary>
    /// Introspects a Keycloak token.
    /// </summary>
    /// <param name="accessToken">The access token to introspect.</param>
    /// <param name="keycloakOptions">The Keycloak options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The introspection result.</returns>
    Task<Result<KeycloakTokenIntrospectionResult>> IntrospectAsync(
        string accessToken,
        KeycloakOptions keycloakOptions,
        CancellationToken cancellationToken);
}
