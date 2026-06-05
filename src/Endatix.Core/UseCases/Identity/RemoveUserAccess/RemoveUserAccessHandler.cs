using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.RemoveUserAccess;

/// <summary>
/// Handler for the <see cref="RemoveUserAccessCommand"/> class.
/// </summary>
public sealed class RemoveUserAccessHandler(IUserService userService)
    : ICommandHandler<RemoveUserAccessCommand, Result>
{
    /// <inheritdoc/>
    public Task<Result> Handle(RemoveUserAccessCommand request, CancellationToken cancellationToken) => userService.RemoveUserAccessAsync(request.UserId, cancellationToken);
}
