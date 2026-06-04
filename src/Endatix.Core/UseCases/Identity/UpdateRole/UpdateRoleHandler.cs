using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.UpdateRole;

/// <summary>
/// Handler for the <see cref="UpdateRoleCommand"/> class.
/// </summary>
public sealed class UpdateRoleHandler(IRoleManagementService roleManagementService)
    : ICommandHandler<UpdateRoleCommand, Result<string>>
{
    /// <inheritdoc/>
    public Task<Result<string>> Handle(UpdateRoleCommand request, CancellationToken cancellationToken)
        => roleManagementService.UpdateRoleAsync(
            request.RoleName,
            request.Description,
            request.Permissions,
            cancellationToken);
}
