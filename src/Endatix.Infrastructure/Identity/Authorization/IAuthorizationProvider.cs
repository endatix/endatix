using Microsoft.AspNetCore.Authorization;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Interface for authorization providers. Authorization providers are responsible for resolving authorization data from the authentication provider and enriching the claims principal with RBAC roles and permissions.
/// </summary>
public interface IAuthorizationProvider
{
    void Configure(AuthorizationOptions options);
}