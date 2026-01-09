namespace Endatix.Core.Abstractions.Submissions;

/// <summary>
/// Claims extracted from a validated access token.
/// </summary>
/// <param name="SubmissionId">The submission ID from the token</param>
/// <param name="Permissions">Collection of granted permissions</param>
/// <param name="ExpiresAt">UTC timestamp when the token expires</param>
public record SubmissionAccessTokenClaims(long SubmissionId, IEnumerable<string> Permissions, DateTime ExpiresAt);
