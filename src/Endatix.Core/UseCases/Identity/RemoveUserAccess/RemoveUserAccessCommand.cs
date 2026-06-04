using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.RemoveUserAccess;

/// <summary>
/// Command to remove a user's access to the current tenant.
/// </summary>
public sealed record RemoveUserAccessCommand : ICommand<Result>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RemoveUserAccessCommand"/> class.
    /// </summary>
    /// <param name="userId">The ID of the user to remove access for.</param>
    public RemoveUserAccessCommand(long userId)
    {
        Guard.Against.NegativeOrZero(userId);
        UserId = userId;
    }

    public long UserId { get; }
}
