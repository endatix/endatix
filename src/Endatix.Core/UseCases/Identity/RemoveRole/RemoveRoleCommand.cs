using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.RemoveRole;

/// <summary>
/// Command to remove a role from a user.
/// </summary>
public record RemoveRoleCommand : ICommand<Result<string>>
{
    /// <summary>
    /// The ID of the user to remove the role from.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// The name of the role to remove.
    /// </summary>
    public string RoleName { get; init; }

    public RemoveRoleCommand(long userId, string roleName)
    {
        Guard.Against.NegativeOrZero(userId, nameof(userId));
        Guard.Against.NullOrWhiteSpace(roleName, nameof(roleName));

        UserId = userId;
        RoleName = roleName;
    }
}
