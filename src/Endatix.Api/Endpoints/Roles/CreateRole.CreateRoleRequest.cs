namespace Endatix.Api.Endpoints.Roles;

/// <summary>
/// Request for creating a new role.
/// </summary>
public record CreateRoleRequest
{
    /// <summary>
    /// The name of the role to create.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// The description of the role.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The list of permission names to assign to the role.
    /// </summary>
    public List<string> Permissions { get; init; } = new();
}
