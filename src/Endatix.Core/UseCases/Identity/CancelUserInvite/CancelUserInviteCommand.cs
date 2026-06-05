using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.CancelUserInvite;

/// <summary>
/// Command to cancel a user invite.
/// </summary>
public sealed record CancelUserInviteCommand : ICommand<Result>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CancelUserInviteCommand"/> class.
    /// </summary>
    /// <param name="userId">The ID of the user to cancel the invite for.</param>
    public CancelUserInviteCommand(long userId)
    {
        Guard.Against.NegativeOrZero(userId);
        UserId = userId;
    }

    public long UserId { get; }
}
