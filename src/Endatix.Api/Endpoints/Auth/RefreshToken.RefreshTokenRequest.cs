using FastEndpoints;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Represents the request for the "/refresh-token" endpoint
/// </summary>
public record RefreshTokenRequest(string? Authorization, string? RefreshToken)
{
    /// <summary>
    /// The Authorization header containing the JWT token. It should be in the format "Bearer {token}".
    /// </summary>
    [FromHeader]
    public string? Authorization { get; init; } = Authorization;

    /// <summary>
    /// The refresh token used to obtain a new access token.
    /// </summary>
    public string? RefreshToken { get; init; } = RefreshToken;
}
