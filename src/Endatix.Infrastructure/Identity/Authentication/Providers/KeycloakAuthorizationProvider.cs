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
    public Task<Result<AuthorizationData>> GetAuthorizationDataAsync(ClaimsPrincipal principal, CancellationToken cancellationToken = default)
    {
        if (!CanHandle(principal))
        {
            return Task.FromResult(Result<AuthorizationData>.Error("Provider cannot handle the given issuer"));
        }

        return Task.FromResult(Result<AuthorizationData>.Success(AuthorizationData.ForAnonymousUser(AuthConstants.DEFAULT_TENANT_ID)));
    }
}