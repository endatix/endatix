using System.Security.Claims;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity;

namespace Endatix.Core.Abstractions;

/// <summary>
/// Defines operations related to token management, including issuing and revoking tokens for users.
/// </summary>
public interface ITokenService
{
    /// <summary>
    /// Issues a token for the specified user, optionally for a specific audience.
    /// </summary>
    /// <param name="forUser">The user for whom the token is being issued.</param>
    /// <param name="forAudience">The audience for whom the token is intended, if any.</param>
    /// <returns>A TokenDto representing the issued token.</returns>
    TokenDto IssueToken(User forUser, string? forAudience = null);

    /// <summary>
    /// Revokes tokens associated with the specified user.
    /// </summary>
    /// <param name="forUser">The user whose tokens are being revoked.</param>
    /// <param name="cancellationToken">A cancellation token to monitor for cancellation requests.</param>
    /// <returns>A Result indicating the outcome of the revocation operation.</returns>
    Task<Result> RevokeTokensAsync(User forUser, CancellationToken cancellationToken = default);
}