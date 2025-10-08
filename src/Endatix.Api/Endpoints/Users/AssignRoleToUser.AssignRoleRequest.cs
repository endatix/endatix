namespace Endatix.Api.Endpoints.Users;

/// <summary>
/// Request for assigning a role to a user.
/// </summary>
public record AssignRoleRequest
{
    /// <summary>
    /// The ID of the user to assign the role to.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// The name of the role to assign.
    /// </summary>
    public string RoleName { get; init; } = string.Empty;
}
