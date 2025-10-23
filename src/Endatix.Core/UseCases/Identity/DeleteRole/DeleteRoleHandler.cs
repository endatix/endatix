using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.DeleteRole;

/// <summary>
/// Handles the deletion of a role.
/// </summary>
public class DeleteRoleHandler(IRoleManagementService roleManagementService) : ICommandHandler<DeleteRoleCommand, Result<string>>
{
    /// <summary>
    /// Handles the DeleteRoleCommand to delete a role.
    /// </summary>
    /// <param name="request">The DeleteRoleCommand containing the role name.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the work.</param>
    /// <returns>A Result indicating success or failure with a message.</returns>
    public async Task<Result<string>> Handle(DeleteRoleCommand request, CancellationToken cancellationToken)
    {
        var result = await roleManagementService.DeleteRoleAsync(request.RoleName, cancellationToken);

        if (!result.IsSuccess)
        {
            return result;
        }

        return Result.Success($"Role '{request.RoleName}' successfully deleted.");
    }
}
