using System.Security.Claims;
using Endatix.Core.Abstractions.Authorization;
using Endatix.Core.UseCases.Identity.ExitAssume;
using Microsoft.AspNetCore.Http;

namespace Endatix.Infrastructure.Identity.Authorization;

internal static class ReturnHomeAuthorization
{
    private static readonly object _grant = new();

    internal static bool Applies(HttpContext? http) => http?.GetEndpoint()?.Metadata.GetMetadata<ReturnHomeSessionMetadata>() is not null;

    internal static async Task<AuthorizationData?> ReadGrantsAsync(HttpContext http, ClaimsPrincipal principal, IAuthorizationStrategy strategy)
    {
        http.Items.Remove(_grant);
        var result = await strategy.GetAuthorizationDataAsync(principal, http.RequestAborted);
        if (!result.IsSuccess)
        {
            return null;
        }
        if (result.Value.Roles.Contains(SystemRole.PlatformAdmin.Name))
        {
            http.Items[_grant] = principal;
        }
        return result.Value with { RequiredActionsUnavailable = true, RequiredActions = [] };
    }

    internal static bool Admits(HttpContext http, ClaimsPrincipal principal) =>
        http.Items.TryGetValue(_grant, out var granted) && ReferenceEquals(granted, principal);
}
