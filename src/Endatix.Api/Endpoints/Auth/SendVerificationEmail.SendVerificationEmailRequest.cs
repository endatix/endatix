namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Represents the request for the "/send-verification-email" endpoint.
/// </summary>
public record SendVerificationEmailRequest(string Email)
{
    /// <summary>
    /// The email address to send the verification email to.
    /// </summary>
    public string Email { get; init; } = Email;
} 