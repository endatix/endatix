using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.RemoveRole;

/// <summary>
/// Handles the removal of a role from a user.
/// </summary>
public class RemoveRoleHandler(IRoleManagementService roleManagementService) : ICommandHandler<RemoveRoleCommand, Result<string>>
{
    /// <summary>
    /// Handles the RemoveRoleCommand to remove a role from a user.
    /// </summary>
    /// <param name="request">The RemoveRoleCommand containing the user ID and role name.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the work.</param>
    /// <returns>A Result indicating success or failure with a message.</returns>
    public async Task<Result<string>> Handle(RemoveRoleCommand request, CancellationToken cancellationToken)
    {
        var result = await roleManagementService.RemoveRoleFromUserAsync(request.UserId, request.RoleName, cancellationToken);

        if (!result.IsSuccess)
        {
            return result;
        }

        return Result.Success($"Role '{request.RoleName}' successfully removed from user.");
    }
}
