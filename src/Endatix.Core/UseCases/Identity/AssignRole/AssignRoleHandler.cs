using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.AssignRole;

/// <summary>
/// Handles the assignment of a role to a user.
/// </summary>
public class AssignRoleHandler(IRoleManagementService roleManagementService) : ICommandHandler<AssignRoleCommand, Result<string>>
{
    /// <summary>
    /// Handles the AssignRoleCommand to assign a role to a user.
    /// </summary>
    /// <param name="request">The AssignRoleCommand containing the user ID and role name.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the work.</param>
    /// <returns>A Result indicating success or failure with a message.</returns>
    public async Task<Result<string>> Handle(AssignRoleCommand request, CancellationToken cancellationToken)
    {
        var result = await roleManagementService.AssignRoleToUserAsync(request.UserId, request.RoleName, cancellationToken);

        if (!result.IsSuccess)
        {
            return result;
        }

        return Result.Success($"Role '{request.RoleName}' successfully assigned to user.");
    }
}
