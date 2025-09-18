using Microsoft.AspNetCore.Authorization;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Authorization handler that grants access to all requirements for admin users
/// </summary>
public class AdminHandler : IAuthorizationHandler
{
    public Task HandleAsync(AuthorizationHandlerContext context)
    {
        var isAdminClaim = context.User.FindFirst(ClaimNames.IsAdmin)?.Value;
        var isAdmin = !string.IsNullOrEmpty(isAdminClaim) && isAdminClaim.Equals("true", StringComparison.OrdinalIgnoreCase);
        if (isAdmin)
        {
            foreach (var requirement in context.Requirements)
            {
                context.Succeed(requirement);
            }
        }

        return Task.CompletedTask;
    }
}
