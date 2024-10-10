using FastEndpoints;

namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Represents the request for the "/refresh-token" endpoint
/// </summary>
public record RefreshTokenRequest(string? Authorization, string? RefreshToken)
{
    [FromHeader]
    public string? Authorization { get; init; } = Authorization;

    public string? RefreshToken { get; init; } = RefreshToken;
}
