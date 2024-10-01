namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Represents the response model for the logout operation.
/// </summary>
/// <param name="Message">The message to be included in the response.</param>
public record LogoutResponse(string Message)
{
    /// <summary>
    /// Gets the message included in the Logout response.
    /// </summary>
    public string Message { get; init; } = Message;
}