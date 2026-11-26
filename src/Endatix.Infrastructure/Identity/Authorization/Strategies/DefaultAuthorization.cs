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

        return activeProvider is EndatixUserJwtAuthProvider;
    }

    /// <inheritdoc />
    public async Task<Result<AuthorizationData>> GetAuthorizationDataAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var data = await ReadVerifiedSessionAsync(principal, cancellationToken);
        return SessionAuthorization.WithoutProof(data, principal);
    }

    internal async Task<Result<AuthorizationData>> ReadVerifiedSessionAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (!CanHandle(principal))
        {
            return Result.Error("Provider cannot handle the given issuer");
        }

        if (!long.TryParse(principal.GetUserId(), out var endatixUserId))
        {
            return Result.Error("User ID is required");
        }

        var tenantId = principal.GetTenantId();
        if (tenantId <= 0)
        {
            return Result.Error("Tenant ID is required");
        }

        return await ReadAsync(SessionFor(principal, endatixUserId, tenantId), cancellationToken);
    }

    private static DefaultAuthorizationDataProvider.Session SessionFor(ClaimsPrincipal principal, long userId, long tenantId)
    {
        var actor = principal.GetActorUserId();
        return new(userId, tenantId, actor)
        { MalformedActor = principal.HasActorClaim() && (actor is null || principal.Identities.First().FindAll(ClaimNames.Actor).Skip(1).Any()) };
    }

    private async Task<Result<AuthorizationData>> ReadAsync(DefaultAuthorizationDataProvider.Session session, CancellationToken ct)
    {
        if (authorizationDataProvider is DefaultAuthorizationDataProvider builtIn)
        {
            return await builtIn.ReadSessionAsync(session, ct);
        }
        var result = await authorizationDataProvider.GetAuthorizationDataAsync(session.UserId, session.TenantId, ct, session.Actor);
        return result.IsSuccess
            ? Result.Success(result.Value with { VerifiedAssumption = null, UnverifiedActor = session.PresentsActor })
            : result;
    }
}
