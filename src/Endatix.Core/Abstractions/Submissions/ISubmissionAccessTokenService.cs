using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions.Submissions;

/// <summary>
/// Service for generating and validating short-lived submission access tokens.
/// Tokens are stateless and signed using HMAC-SHA256.
/// </summary>
public interface ISubmissionAccessTokenService
{
    /// <summary>
    /// Generates a short-lived access token for a submission.
    /// </summary>
    /// <param name="submissionId">The ID of the submission</param>
    /// <param name="expiryMinutes">Expiry time in minutes (recommended: 5-10080)</param>
    /// <param name="permissions">Collection of permissions: "view", "edit", "export"</param>
    /// <returns>Token data with expiry information</returns>
    Result<SubmissionAccessTokenDto> GenerateAccessToken(long submissionId, int expiryMinutes, IEnumerable<string> permissions);

    /// <summary>
    /// Validates an access token and extracts its claims.
    /// </summary>
    /// <param name="token">The token to validate</param>
    /// <returns>Token claims if valid, or error result</returns>
    Result<SubmissionAccessTokenClaims> ValidateAccessToken(string token);
}


