using Endatix.Core.Abstractions.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Endatix.Infrastructure.Identity.Authorization.Handlers;

/// <summary>
/// Handles authorization for TenantAdminRequirement.
/// Verifies user has Admin role within their tenant scope.
/// </summary>
public sealed class TenantAdminHandler(IPermissionService permissionService) : AuthorizationHandler<TenantAdminRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantAdminRequirement requirement)
    {
        var currentUser = context.User;
        if (currentUser is null)
        {
            return;
        }

        var claimCheckResult = currentUser.IsAdmin();
        if (claimCheckResult.IsSuccess && claimCheckResult.Value)
        {
            context.Succeed(requirement);
            return;
        }

        var userId = currentUser.GetUserId();
        if (userId is null || !long.TryParse(userId, out var parsedUserId))
        {
            return;
        }

        var result = await permissionService.IsUserAdminAsync(parsedUserId);
        if (result.IsSuccess && result.Value)
        {
            context.Succeed(requirement);
            return;
        }

        return;
    }
}