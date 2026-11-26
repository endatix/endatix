using System.Security.Claims;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity.Authorization.Strategies;

namespace Endatix.Infrastructure.Identity.Authorization;

internal static class SessionAuthorization
{
    internal static Task<AuthorizationData> ReadAsync(this IAuthorizationCache cache, ClaimsPrincipal principal,
        IAuthorizationStrategy strategy, CancellationToken ct) =>
        cache is AuthorizationCache builtInCache && strategy is DefaultAuthorization builtIn
            ? builtInCache.GetOrCreateAsync(principal, token => builtIn.ReadVerifiedSessionAsync(principal, token), ct)
            : cache.GetOrCreateAsync(principal, token => strategy.GetAuthorizationDataAsync(principal, token), ct);

    internal static Result<AuthorizationData> WithoutProof(Result<AuthorizationData> data) =>
        data.IsSuccess && data.Value.VerifiedAssumption is not null ? Result.Success(data.Value with { VerifiedAssumption = null }) : data;
}
