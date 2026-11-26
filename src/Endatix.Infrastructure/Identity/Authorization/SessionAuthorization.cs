using System.Security.Claims;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Authorization.Strategies;

namespace Endatix.Infrastructure.Identity.Authorization;

internal static class SessionAuthorization
{
    internal static async Task<AuthorizationData> ReadAsync(this IAuthorizationCache cache, ClaimsPrincipal principal,
        IAuthorizationStrategy strategy, CancellationToken ct)
    {
        if (cache is AuthorizationCache builtInCache && strategy is DefaultAuthorization builtIn)
        {
            return await builtInCache.GetOrCreateAsync(principal, token => builtIn.ReadVerifiedSessionAsync(principal, token), ct);
        }
        var data = await cache.GetOrCreateAsync(principal, token => strategy.GetAuthorizationDataAsync(principal, token), ct);
        // A replacement may return a snapshot without invoking the factory on this request.
        if (principal.HasActorClaim())
        {
            return data with { VerifiedAssumption = null, RequiredActionsUnavailable = true, RequiredActions = [] };
        }
        return data.VerifiedAssumption is null ? data : data with { VerifiedAssumption = null };
    }

    internal static Result<AuthorizationData> WithoutProof(Result<AuthorizationData> data, ClaimsPrincipal? principal)
    {
        if (!data.IsSuccess || (data.Value.VerifiedAssumption is null && principal?.HasActorClaim() != true))
        {
            return data;
        }
        return Result.Success(data.Value with { VerifiedAssumption = null, UnverifiedActor = principal?.HasActorClaim() == true });
    }
}
