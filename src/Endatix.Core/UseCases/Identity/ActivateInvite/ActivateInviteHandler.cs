using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.ActivateInvite;

public sealed class ActivateInviteHandler(IEmailVerificationService emailVerificationService)
    : ICommandHandler<ActivateInviteCommand, Result<User>>
{
    /// <inheritdoc/>
    public Task<Result<User>> Handle(
        ActivateInviteCommand request,
        CancellationToken cancellationToken)
    {
        return emailVerificationService.ActivateInviteAsync(
            request.Token,
            request.Password,
            cancellationToken);
    }
}
