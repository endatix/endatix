namespace Endatix.Api.Endpoints.Users;

/// <summary>
/// Request for removing a role from a user.
/// </summary>
public record RemoveRoleRequest
{
    /// <summary>
    /// The ID of the user to remove the role from.
    /// </summary>
    public long UserId { get; init; }

    /// <summary>
    /// The name of the role to remove.
    /// </summary>
    public string RoleName { get; init; } = string.Empty;
}
