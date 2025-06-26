using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions;

/// <summary>
/// Defines the contract for email verification operations.
/// </summary>
public interface IEmailVerificationService
{
    /// <summary>
    /// Creates an email verification token for the specified user.
    /// </summary>
    /// <param name="userId">The ID of the user to create a token for.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation if needed.</param>
    /// <returns>A Result containing the created EmailVerificationToken if successful.</returns>
    Task<Result<EmailVerificationToken>> CreateVerificationTokenAsync(long userId, CancellationToken cancellationToken);

    /// <summary>
    /// Verifies a user's email and invalidates the verification token.
    /// </summary>
    /// <param name="token">The verification token.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation if needed.</param>
    /// <returns>A Result containing the verified User if successful.</returns>
    Task<Result<User>> VerifyEmailAsync(string token, CancellationToken cancellationToken);
} 