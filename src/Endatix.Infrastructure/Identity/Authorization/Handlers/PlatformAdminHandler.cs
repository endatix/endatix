using System.Security.Claims;
using Endatix.Core.Abstractions.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace Endatix.Infrastructure.Identity.Authorization.Handlers;

/// <summary>
/// Handles authorization for PlatformAdminRequirement.
/// Verifies user has PlatformAdmin role for cross-tenant access.
/// </summary>
public sealed class PlatformAdminHandler(ICurrentUserAuthorizationService authorizationService)
    : AuthorizationHandler<PlatformAdminRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PlatformAdminRequirement requirement)
    {
        if (context?.User is not ClaimsPrincipal currentUser || currentUser.Identity is not ClaimsIdentity)
        {
            return;
        }

        var admitted = context.Resource is HttpContext http && ReturnHomeAuthorization.Applies(http)
            ? ReturnHomeAuthorization.Admits(http, currentUser)
            : await HasPlatformGrantAsync(currentUser);
        if (admitted)
        {
            context.Succeed(requirement);
        }
    }

    private async Task<bool> HasPlatformGrantAsync(ClaimsPrincipal currentUser)
    {
        if (currentUser.IsHydrated())
        {
            return currentUser.IsInRole(SystemRole.PlatformAdmin.Name);
        }
        var result = await authorizationService.IsPlatformAdminAsync(CancellationToken.None);
        return result.IsSuccess && result.Value;
    }
}
