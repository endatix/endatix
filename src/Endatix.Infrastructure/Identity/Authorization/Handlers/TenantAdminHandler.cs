using Endatix.Core.Abstractions;
using Microsoft.AspNetCore.Authorization;

namespace Endatix.Infrastructure.Identity.Authorization.Handlers;

/// <summary>
/// Handles authorization for TenantAdminRequirement.
/// Verifies user has Admin role within their tenant scope.
/// </summary>
public sealed class TenantAdminHandler(IUserContext userContext, IPermissionService permissionService) : AuthorizationHandler<TenantAdminRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantAdminRequirement requirement)
    {
        var userId = userContext.GetCurrentUserId();
        if (userId == null || !long.TryParse(userId, out var parsedUserId))
        {
            return;
        }

        var result = await permissionService.IsUserAdminAsync(parsedUserId);
        if (result.IsSuccess && result.Value)
        {
            context.Succeed(requirement);
        }

        var platformAdminResult = await permissionService.IsUserPlatformAdminAsync(parsedUserId);
        if (platformAdminResult.IsSuccess && platformAdminResult.Value)
        {
            context.Succeed(requirement);
        }
    }
}