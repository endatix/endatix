using System.Security.Claims;
using Endatix.Core.Abstractions.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Endatix.Infrastructure.Identity.Authorization.Handlers;

/// <summary>
/// Handles authorization for TenantAdminRequirement.
/// Verifies user has Admin role within their tenant scope.
/// </summary>
public sealed class TenantAdminHandler(ICurrentUserAuthorizationService authorizationService) : AuthorizationHandler<TenantAdminRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        TenantAdminRequirement requirement)
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
            var isAdmin = currentUser.IsAdmin();
            if (isAdmin.IsSuccess && isAdmin.Value)
            {
                context.Succeed(requirement);
            }

            return;
        }
        else
        {
            var isAdminResult = await authorizationService.IsAdminAsync(CancellationToken.None);
            if (isAdminResult.IsSuccess && isAdminResult.Value)
            {
                context.Succeed(requirement);
            }
        }

        return;
    }
}