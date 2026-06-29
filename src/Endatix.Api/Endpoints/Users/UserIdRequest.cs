namespace Endatix.Api.Endpoints.Users;

/// <summary>
/// Route request containing a user ID.
/// </summary>
public sealed record UserIdRequest
{
    /// <summary>
    /// The user identifier from the route.
    /// </summary>
    public long UserId { get; init; }
}