using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.ReplaceUserRoles;

/// <summary>
/// Command to replace a user's editable tenant role set.
/// </summary>
public sealed record ReplaceUserRolesCommand : ICommand<Result<string>>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ReplaceUserRolesCommand"/> class.
    /// </summary>
    /// <param name="userId">The user ID.</param>
    /// <param name="roleNames">The full replacement role set.</param>
    public ReplaceUserRolesCommand(long userId, IReadOnlyList<string> roleNames)
    {
        Guard.Against.NegativeOrZero(userId, nameof(userId));
        Guard.Against.Null(roleNames, nameof(roleNames));

        UserId = userId;
        RoleNames = roleNames;
    }

    public long UserId { get; }
    public IReadOnlyList<string> RoleNames { get; }
}
