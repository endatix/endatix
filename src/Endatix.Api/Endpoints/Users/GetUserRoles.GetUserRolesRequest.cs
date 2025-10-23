namespace Endatix.Api.Endpoints.Users;

/// <summary>
/// Request for retrieving roles assigned to a user.
/// </summary>
public record GetUserRolesRequest
{
    /// <summary>
    /// The ID of the user to get roles for.
    /// </summary>
    public long UserId { get; init; }
}
