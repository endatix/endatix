namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Response containing the generated access token.
/// </summary>
public record CreateAccessTokenResponse(string Token, DateTime ExpiresAt, IEnumerable<string> Permissions);
