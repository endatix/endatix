using Endatix.Core.Abstractions;
using Microsoft.AspNetCore.Authorization;

namespace Endatix.Infrastructure.Identity.Authorization.Handlers;

/// <summary>
/// Handles authorization for PlatformAdminRequirement.
/// Verifies user has PlatformAdmin role for cross-tenant access.
/// </summary>
public sealed class PlatformAdminHandler(IUserContext userContext, IPermissionService permissionService)
    : AuthorizationHandler<PlatformAdminRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PlatformAdminRequirement requirement)
    {
        var userId = userContext.GetCurrentUserId();
        if (userId == null || !long.TryParse(userId, out var parsedUserId))
        {
            return;
        }

        var result = await permissionService.IsUserPlatformAdminAsync(parsedUserId);
        if (result.IsSuccess)
        {
            context.Succeed(requirement);
        }
    }
}