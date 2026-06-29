using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.CancelUserInvite;

/// <summary>
/// Handler for the <see cref="CancelUserInviteCommand"/> class.
/// </summary>
public sealed class CancelUserInviteHandler(IUserService userService)
    : ICommandHandler<CancelUserInviteCommand, Result>
{
    /// <inheritdoc/>
    public Task<Result> Handle(CancelUserInviteCommand request, CancellationToken cancellationToken)
        => userService.CancelUserInviteAsync(request.UserId, cancellationToken);
}
