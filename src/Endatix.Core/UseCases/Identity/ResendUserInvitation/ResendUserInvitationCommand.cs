using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.ResendUserInvitation;

/// <summary>
/// Command to resend a user invitation email.
/// </summary>
public sealed record ResendUserInvitationCommand : ICommand<Result>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ResendUserInvitationCommand"/> class.
    /// </summary>
    /// <param name="userId">The ID of the user to resend the invitation email to.</param>
    public ResendUserInvitationCommand(long userId)
    {
        Guard.Against.NegativeOrZero(userId);
        UserId = userId;
    }

    public long UserId { get; }
}
