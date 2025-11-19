using System.Security.Claims;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Defines the contract for authorization strategies that extract roles and permissions
/// from external authentication providers.
/// </summary>
public interface IAuthorizationStrategy
{
    /// <summary>
    /// Checks if the provider can handle the authentication provider of the given claims principal.
    /// </summary>
    /// <param name="principal">The claims principal to check.</param>
    /// <returns>True if the provider can handle the given claims principal, false otherwise.</returns>
    bool CanHandle(ClaimsPrincipal principal);

    /// <summary>
    /// Extracts authorization data (roles, permissions) from the authentication context.
    /// </summary>
    /// <param name="principal">The claims principal from authentication.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Result containing authorization data, or null if strategy cannot handle the principal.</returns>
    Task<Result<AuthorizationData>> GetAuthorizationDataAsync(
        ClaimsPrincipal principal,
        CancellationToken cancellationToken);
}