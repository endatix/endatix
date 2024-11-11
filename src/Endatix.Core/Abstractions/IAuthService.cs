using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions;

/// <summary>
/// Defines the interface for authentication services.
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// Validates user credentials.
    /// </summary>
    /// <param name="email">The email of the user.</param>
    /// <param name="password">The password of the user.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the validation result.</returns>
    Task<Result<User>> ValidateCredentials(string email, string password, CancellationToken cancellationToken);

    /// <summary>
    /// Validates a refresh token for a user.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="refreshToken">The refresh token to validate.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the validation result.</returns>
    Task<Result<User>> ValidateRefreshToken(long userId, string refreshToken, CancellationToken cancellationToken);

    /// <summary>
    /// Stores a refresh token for a user.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="token">The refresh token to store.</param>
    /// <param name="expireAt">The expiration date and time of the token.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>A task that represents the asynchronous operation. The task result contains the storage result.</returns>
    Task<Result> StoreRefreshToken(long userId, string token, DateTime expireAt, CancellationToken cancellationToken);
}
