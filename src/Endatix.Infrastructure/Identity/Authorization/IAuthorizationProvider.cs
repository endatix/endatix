using System.Security.Claims;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Interface for authorization providers. Authorization providers are responsible for resolving authorization data from the authentication provider and enriching the claims principal with RBAC roles and permissions.
/// </summary>
public interface IAuthorizationProvider
{
    bool CanHandle(ClaimsPrincipal principal);
    
    Task<Result<AuthorizationData>> GetAuthorizationDataAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default);
}