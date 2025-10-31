using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.DeleteRole;

/// <summary>
/// Command to delete a role.
/// </summary>
public record DeleteRoleCommand : ICommand<Result<string>>
{
    /// <summary>
    /// The name of the role to delete.
    /// </summary>
    public string RoleName { get; init; }

    public DeleteRoleCommand(string roleName)
    {
        Guard.Against.NullOrWhiteSpace(roleName, nameof(roleName));

        RoleName = roleName;
    }
}
