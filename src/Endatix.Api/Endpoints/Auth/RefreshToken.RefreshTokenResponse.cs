namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Represents the response for the "/refresh-token" endpoint
/// </summary>
public record RefreshTokenResponse(string AccessToken, string RefreshToken)
{
    /// <summary>
    /// Access token returned upon success
    /// </summary>
    public string AccessToken { get; init; } = AccessToken;

    /// <summary>
    /// Refresh token returned upon success
    /// </summary>
    public string RefreshToken { get; init; } = RefreshToken;
}
