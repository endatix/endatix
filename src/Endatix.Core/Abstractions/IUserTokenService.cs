using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity;

namespace Endatix.Core.Abstractions;

/// <summary>
/// Defines operations related to token management, including issuing and revoking tokens for users.
/// </summary>
public interface IUserTokenService
{
    /// <summary>
    /// Issues an access token for the specified user, optionally for a specific audience.
    /// </summary>
    /// <param name="forUser">The user for whom the token is being issued.</param>
    /// <param name="forAudience">The audience for whom the token is intended, if any.</param>
    /// <returns>A TokenDto representing the issued access token.</returns>
    TokenDto IssueAccessToken(User forUser, string? forAudience = null);

    /// <summary>
    /// Validates an access token and returns the user ID if the token is valid.
    /// </summary>
    /// <param name="accessToken">The access token to validate.</param>
    /// <param name="validateLifetime">Indicates whether the token's lifetime should be validated. Default value is true.</param>
    /// <returns>A Result containing the user ID if the token is valid.</returns>
    Task<Result<long>> ValidateAccessTokenAsync(string accessToken, bool validateLifetime = true);

    /// <summary>
    /// Revokes tokens associated with the specified user.
    /// </summary>
    /// <param name="forUser">The user whose tokens are being revoked.</param>
    /// <param name="cancellationToken">A cancellation token to monitor for cancellation requests.</param>
    /// <returns>A Result indicating the outcome of the revocation operation.</returns>
    Task<Result> RevokeTokensAsync(User forUser, CancellationToken cancellationToken = default);

    // /// <summary>
    // /// Issues a refresh token.
    // /// </summary>
    // /// <returns>A TokenDto representing the issued refresh token.</returns>
    TokenDto IssueRefreshToken();
}
