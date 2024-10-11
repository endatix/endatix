namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// The response type for the "/register" endpoint returned by the <see cref="Register.HandleAsync"/> method.
/// </summary>
public record RegisterResponse(bool Success, string Message)
{
    /// <summary>
    /// Indicates whether the registration was successful.
    /// </summary>
    public bool Success { get; init; } = Success;

    /// <summary>
    /// A message describing the outcome of the registration attempt.
    /// </summary>
    public string Message { get; init; } = Message;
}
