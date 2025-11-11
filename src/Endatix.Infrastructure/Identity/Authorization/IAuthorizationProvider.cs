using System.Security.Claims;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Interface for authorization providers. Authorization providers are responsible for resolving authorization data from the authentication provider and enriching the claims principal with RBAC roles and permissions.
/// </summary>
public interface IAuthorizationProvider
{
    /// <summary>
    /// Checks if the provider can handle the authentication provider of the given claims principal.
    /// </summary>
    /// <param name="principal">The claims principal to check.</param>
    /// <returns>True if the provider can handle the given claims principal, false otherwise.</returns>
    bool CanHandle(ClaimsPrincipal principal);

    /// <summary>
    /// Gets the authorization data for the given claims principal.
    /// </summary>
    /// <param name="principal">The claims principal to get authorization data for.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Result containing the authorization data.</returns>
    Task<Result<AuthorizationData>> GetAuthorizationDataAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}