using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions.Submissions;

/// <summary>
/// Service for generating and validating submission access tokens.
/// Tokens are stateless and signed using HMAC-SHA256.
/// </summary>
public interface ISubmissionAccessTokenService
{
    /// <summary>The longest lifetime a token is issued with: 60 days.</summary>
    const int MaxExpiryMinutes = 60 * 24 * 60;

    /// <summary>
    /// Generates an access token for a submission.
    /// </summary>
    /// <param name="submissionId">The ID of the submission</param>
    /// <param name="expiryMinutes">Expiry time in minutes (API max: 86400 / 60 days)</param>
    /// <param name="permissions">Collection of permissions: "view", "edit", "export"</param>
    /// <returns>Token data with expiry information</returns>
    Result<SubmissionAccessTokenDto> GenerateAccessToken(long submissionId, int expiryMinutes, IEnumerable<string> permissions);

    /// <summary>
    /// Generates an access token for a submission, bound to <see cref="SubmissionAccessTokenRequest.Stamp"/> when set.
    /// </summary>
    Result<SubmissionAccessTokenDto> GenerateAccessToken(SubmissionAccessTokenRequest request);

    /// <summary>
    /// Validates an access token and extracts its claims: signed, unexpired, and still current, since what it was
    /// issued for may have gone (<see cref="ISubmissionAccessTokenGate"/>). A revoked token is invalid, as an expired
    /// one is.
    /// </summary>
    /// <param name="token">The token to validate</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Token claims if valid, or error result</returns>
    Task<Result<SubmissionAccessTokenClaims>> ValidateAccessTokenAsync(string token, CancellationToken cancellationToken);
}

/// <summary>A token to issue.</summary>
/// <param name="SubmissionId">The submission it opens.</param>
/// <param name="ExpiryMinutes">Its lifetime, at most <see cref="ISubmissionAccessTokenService.MaxExpiryMinutes"/>.</param>
/// <param name="Permissions">"view", "edit", "export", "submit".</param>
/// <param name="Stamp">
/// Optional lowercase hex the token is bound to and signed with, for <see cref="ISubmissionAccessTokenGate"/> to check
/// on every use.
/// </param>
public sealed record SubmissionAccessTokenRequest(
    long SubmissionId,
    int ExpiryMinutes,
    IEnumerable<string> Permissions,
    string? Stamp = null);
