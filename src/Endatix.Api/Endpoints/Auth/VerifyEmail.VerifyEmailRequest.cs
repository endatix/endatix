namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Represents the request for the "/verify-email" endpoint.
/// </summary>
public record VerifyEmailRequest(string Token)
{
    /// <summary>
    /// The verification token.
    /// </summary>
    public string Token { get; init; } = Token;
} 