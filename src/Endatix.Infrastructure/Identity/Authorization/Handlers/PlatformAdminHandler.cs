using System.Security.Claims;
using Endatix.Core.Abstractions.Authorization;
using Microsoft.AspNetCore.Authorization;

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
        if (context?.User is not ClaimsPrincipal currentUser)
        {
            return;
        }

        if (currentUser.Identity is not ClaimsIdentity)
        {
            return;
        }

        var isHydrated = currentUser.IsHydrated();
        if (isHydrated)
        {
            var isPlatformAdmin = currentUser.IsInRole(SystemRole.PlatformAdmin.Name);
            if (isPlatformAdmin)
            {
                context.Succeed(requirement);
            }

            return;
        }
        else
        {
            var isPlatformAdminResult = await authorizationService.IsPlatformAdminAsync(CancellationToken.None);
            if (isPlatformAdminResult.IsSuccess && isPlatformAdminResult.Value)
            {
                context.Succeed(requirement);
            }

            return;
        }
    }
}