using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Endatix.Infrastructure.Identity.Authorization.Handlers;

public sealed class PermissionsAssertionHandler : AuthorizationHandler<AssertionRequirement>
{

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, AssertionRequirement requirement)
    {
        var result = await requirement.Handler(context);
        if (result)
        {
            context.Succeed(requirement);
        }
    }
}