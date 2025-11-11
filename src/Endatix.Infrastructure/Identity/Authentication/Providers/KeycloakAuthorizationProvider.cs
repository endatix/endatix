using System.Security.Claims;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Authorization;

namespace Endatix.Infrastructure.Identity.Authentication.Providers;

public sealed class KeycloakAuthorizationProvider(AuthProviderRegistry authProviderRegistry) : IAuthorizationProvider
{
    /// <inheritdoc />
    public bool CanHandle(ClaimsPrincipal principal)
    {
        var issuer = principal.GetIssuer();
        if (issuer is null)
        {
            return false;
        }

        var activeProvider = authProviderRegistry
            .GetActiveProviders()
            .FirstOrDefault(provider => provider.CanHandle(issuer, string.Empty));

        return activeProvider is not null && activeProvider is KeycloakAuthProvider;
    }

    /// <inheritdoc />
    public async Task<Result<AuthorizationData>> GetAuthorizationDataAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        if (!CanHandle(principal))
        {
            return Result.Error("Provider cannot handle the given issuer");
        }

        var authorizationData = await Task.FromResult(new AuthorizationData
        {
            Roles = ["User"],
            Permissions = ["User"],
            IsAdmin = false,
        });

        return Result.Success(authorizationData);
    }
}