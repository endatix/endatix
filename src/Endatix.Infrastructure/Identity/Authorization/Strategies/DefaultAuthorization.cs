using System.Security.Claims;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Authentication;
using Endatix.Infrastructure.Identity.Authentication.Providers;
using Endatix.Infrastructure.Identity.Authorization.Data;


namespace Endatix.Infrastructure.Identity.Authorization.Strategies;

/// <summary>
/// Default authorization strategy that routes requests to the appropriate authorization reader.
/// </summary>
/// <param name="authProviderRegistry">The authentication provider registry.</param>
/// <param name="authorizationDataProvider">The authorization data provider responsible for fetching user authorization data.</param>
public sealed class DefaultAuthorization(
    AuthProviderRegistry authProviderRegistry,
    IAuthorizationDataProvider authorizationDataProvider) : IAuthorizationStrategy
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

        return activeProvider is not null && activeProvider is EndatixJwtAuthProvider;
    }

    /// <inheritdoc />
    public async Task<Result<AuthorizationData>> GetAuthorizationDataAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {

        if (!CanHandle(principal))
        {
            return Result.Error("Provider cannot handle the given issuer");
        }

        var userId = principal.GetUserId();
        if (userId is null || !long.TryParse(userId, out var endatixUserId))
        {
            return Result.Error("User ID is required");
        }

        return await authorizationDataProvider.GetAuthorizationDataAsync(endatixUserId, cancellationToken);
    }
}