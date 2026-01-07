namespace Endatix.Core.Abstractions.Submissions;

/// <summary>
/// Data transfer object for generated access tokens.
/// </summary>
/// <param name="Token">The generated token string</param>
/// <param name="ExpiresAt">UTC timestamp when the token expires</param>
/// <param name="Permissions">Collection of granted permissions</param>
public record SubmissionAccessTokenDto(string Token, DateTime ExpiresAt, IEnumerable<string> Permissions);
