using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.UpdateRole;

/// <summary>
/// Command to update a role.
/// </summary>
public sealed record UpdateRoleCommand : ICommand<Result<string>>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateRoleCommand"/> class.
    /// </summary>
    /// <param name="roleName">The name of the role to update.</param>
    /// <param name="description">The description of the role.</param>
    /// <param name="permissions">The permissions to assign to the role.</param>
    public UpdateRoleCommand(string roleName, string? description, List<string> permissions)
    {
        Guard.Against.NullOrWhiteSpace(roleName);
        Guard.Against.Null(permissions);

        RoleName = roleName;
        Description = description;
        Permissions = permissions;
    }

    public string RoleName { get; }
    public string? Description { get; }
    public List<string> Permissions { get; }
}
