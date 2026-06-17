using Endatix.Core.Abstractions;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using MediatR;

namespace Endatix.Core.UseCases.PlatformAdmin.RevokePlatformAdmin;

/// <summary>
/// Handles platform administrator revocations.
/// </summary>
public sealed class RevokePlatformAdminHandler(
    IRoleManagementService roleManagementService,
    IMediator mediator)
    : ICommandHandler<RevokePlatformAdminCommand, Result<string>>
{
    /// <inheritdoc/>
    public async Task<Result<string>> Handle(RevokePlatformAdminCommand request, CancellationToken cancellationToken)
    {
        var result = await roleManagementService.RevokePlatformAdminAsync(request.UserId, cancellationToken);
        if (!result.IsSuccess)
        {
            return result.ToErrorResult<string>();
        }

        await mediator.Publish(new PlatformAdminRevokedEvent(request.UserId), cancellationToken);

        return Result.Success("Platform administrator access revoked.");
    }
}
