namespace Endatix.Api.Endpoints.Auth;

/// <summary>
/// Represents the request for the "/refresh-token" endpoint
/// </summary>
public record RefreshTokenRequest(long UserId, string RefreshToken)
{
    public long UserId { get; init; } = UserId;
    public string RefreshToken { get; init; } = RefreshToken;
}
