using Endatix.Core.Abstractions;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using MediatR;

namespace Endatix.Core.UseCases.PlatformAdmin.GrantPlatformAdmin;

/// <summary>
/// Handles platform administrator grants.
/// </summary>
public sealed class GrantPlatformAdminHandler(
    IRoleManagementService roleManagementService,
    IMediator mediator)
    : ICommandHandler<GrantPlatformAdminCommand, Result<string>>
{
    /// <inheritdoc/>
    public async Task<Result<string>> Handle(GrantPlatformAdminCommand request, CancellationToken cancellationToken)
    {
        var result = await roleManagementService.GrantPlatformAdminAsync(request.UserId, cancellationToken);
        if (!result.IsSuccess)
        {
            return result.ToErrorResult<string>();
        }

        await mediator.Publish(new PlatformAdminGrantedEvent(request.UserId), cancellationToken);

        return Result.Success("Platform administrator access granted.");
    }
}
