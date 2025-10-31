using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.CreateRole;

/// <summary>
/// Handles the creation of a new role with permissions.
/// </summary>
public class CreateRoleHandler(IRoleManagementService roleManagementService) : ICommandHandler<CreateRoleCommand, Result<string>>
{
    /// <summary>
    /// Handles the CreateRoleCommand to create a new role with permissions.
    /// </summary>
    /// <param name="request">The CreateRoleCommand containing the role name, description, and permissions.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the work.</param>
    /// <returns>A Result containing the ID of the created role.</returns>
    public async Task<Result<string>> Handle(CreateRoleCommand request, CancellationToken cancellationToken)
    {
        return await roleManagementService.CreateRoleAsync(
            request.Name,
            request.Description,
            request.Permissions,
            cancellationToken);
    }
}
