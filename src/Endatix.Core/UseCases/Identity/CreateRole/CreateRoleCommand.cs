using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.CreateRole;

/// <summary>
/// Command to create a new role with permissions.
/// </summary>
public record CreateRoleCommand : ICommand<Result<string>>
{
    /// <summary>
    /// The name of the role to create.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// The description of the role.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The list of permission names to assign to the role.
    /// </summary>
    public List<string> Permissions { get; init; }

    public CreateRoleCommand(string name, string? description, List<string> permissions)
    {
        Guard.Against.NullOrWhiteSpace(name, nameof(name));
        Guard.Against.NullOrEmpty(permissions, nameof(permissions));

        Name = name;
        Description = description;
        Permissions = permissions;
    }
}
