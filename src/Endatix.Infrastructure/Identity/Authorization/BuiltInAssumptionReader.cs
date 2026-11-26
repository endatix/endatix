using System.Security.Claims;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Infrastructure.Identity.Authorization.Strategies;
using Microsoft.AspNetCore.Http;

namespace Endatix.Infrastructure.Identity.Authorization;

internal sealed class BuiltInAssumptionReader(IHttpContextAccessor context, IEnumerable<IAuthorizationStrategy> strategies) : VerifiedAssumptionReader
{
    internal override async Task<VerifiedAssumption?> ReadAsync(CancellationToken cancellationToken)
    {
        var principal = context.HttpContext?.User;
        if (principal is null)
        {
            return null;
        }
        var original = new ClaimsPrincipal(principal.Identities.Where(i => i is not AuthorizedIdentity));
        if (strategies.FirstOrDefault(strategy => strategy.CanHandle(original)) is not DefaultAuthorization builtIn)
        {
            return null;
        }
        var data = await builtIn.ReadVerifiedSessionAsync(original, cancellationToken);
        return data.IsSuccess && data.Value.UserId == principal.GetUserId() && data.Value.TenantId == principal.GetTenantId()
            ? data.Value.VerifiedAssumption : null;
    }
}
