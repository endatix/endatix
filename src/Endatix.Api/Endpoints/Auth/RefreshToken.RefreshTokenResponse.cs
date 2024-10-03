namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Represents the response for the "/refresh-token" endpoint
/// </summary>
public record RefreshTokenResponse(string AccessToken, string RefreshToken)
{
    public string AccessToken { get; init; } = AccessToken;
    public string RefreshToken { get; init; } = RefreshToken;
}
