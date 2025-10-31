using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.AssignRole;

/// <summary>
/// Command to assign a role to a user.
/// </summary>
public record AssignRoleCommand : ICommand<Result<string>>
{
    /// <summary>
    /// The ID of the user to assign the role to.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// The name of the role to assign.
    /// </summary>
    public string RoleName { get; init; }

    public AssignRoleCommand(long userId, string roleName)
    {
        Guard.Against.NegativeOrZero(userId, nameof(userId));
        Guard.Against.NullOrWhiteSpace(roleName, nameof(roleName));

        UserId = userId;
        RoleName = roleName;
    }
}
