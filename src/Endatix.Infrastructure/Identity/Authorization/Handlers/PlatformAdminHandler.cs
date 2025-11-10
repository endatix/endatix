using Endatix.Core.Abstractions.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Endatix.Infrastructure.Identity.Authorization.Handlers;

/// <summary>
/// Handles authorization for PlatformAdminRequirement.
/// Verifies user has PlatformAdmin role for cross-tenant access.
/// </summary>
public sealed class PlatformAdminHandler(IPermissionService permissionService)
    : AuthorizationHandler<PlatformAdminRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PlatformAdminRequirement requirement)
    {
        var currentUser = context.User;
        if (currentUser is null)
        {
            return;
        }

        var isPlatformAdmin = currentUser.IsHydrated() && currentUser.IsInRole(SystemRole.PlatformAdmin.Name);
        if (isPlatformAdmin)
        {
            context.Succeed(requirement);
            return;
        }

        var userId = currentUser.GetUserId();
        if (userId == null || !long.TryParse(userId, out var parsedUserId))
        {
            return;
        }

        var result = await permissionService.IsUserPlatformAdminAsync(parsedUserId);
        if (result.IsSuccess)
        {
            context.Succeed(requirement);
            return;
        }

        return;
    }
}