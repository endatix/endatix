namespace Endatix.Api.Endpoints.Users;

/// <summary>
/// Response model for a user in the list users endpoint.
/// </summary>
public record ListUsersResponse
{
    /// <summary>
    /// The user's unique identifier.
    /// </summary>
    public long Id { get; init; }

    /// <summary>
    /// The user's display name.
    /// </summary>
    public string UserName { get; init; } = null!;

    /// <summary>
    /// The user's email address.
    /// </summary>
    public string Email { get; init; } = null!;

    /// <summary>
    /// Indicates whether the user's email is verified.
    /// </summary>
    public bool IsVerified { get; init; }

    /// <summary>
    /// The role names assigned to the user.
    /// </summary>
    public IReadOnlyList<string> Roles { get; init; } = [];
}
