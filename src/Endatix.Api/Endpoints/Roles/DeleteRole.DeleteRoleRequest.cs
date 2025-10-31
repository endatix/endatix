namespace Endatix.Api.Endpoints.Roles;

/// <summary>
/// Request for deleting a role.
/// </summary>
public record DeleteRoleRequest
{
    /// <summary>
    /// The name of the role to delete.
    /// </summary>
    public string? RoleName { get; init; }
}
