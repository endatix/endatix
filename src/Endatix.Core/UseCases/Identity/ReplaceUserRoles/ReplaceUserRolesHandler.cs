using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.ReplaceUserRoles;

/// <summary>
/// Handles replacing a user's editable tenant role set.
/// </summary>
public sealed class ReplaceUserRolesHandler(IRoleManagementService roleManagementService)
    : ICommandHandler<ReplaceUserRolesCommand, Result<string>>
{
    /// <inheritdoc/>
    public async Task<Result<string>> Handle(ReplaceUserRolesCommand request, CancellationToken cancellationToken)
    {
        var result = await roleManagementService.ReplaceRolesForUserAsync(
            request.UserId,
            request.RoleNames,
            cancellationToken);

        if (!result.IsSuccess)
        {
            return result.ToErrorResult<string>();
        }

        return Result.Success("User roles updated.");
    }
}
