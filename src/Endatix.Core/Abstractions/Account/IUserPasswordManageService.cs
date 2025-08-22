using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions.Account;

/// <summary>
/// Defines operations related to user password management.
/// </summary>
public interface IUserPasswordManageService
{
    /// <summary>
    /// Generates a password reset token for the specified user.
    /// </summary>
    /// <param name="email">The email of the user to generate a password reset token for.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation if needed.</param>
    /// <returns>A Result containing the password reset token if successful.</returns>
    Task<Result<string>> GeneratePasswordResetTokenAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Resets the password for the specified user.
    /// </summary>
    /// <param name="email"></param>
    /// <param name="resetCode"></param>
    /// <param name="newPassword"></param>
    /// <param name="cancellationToken"></param>
    /// <returns>A Result containing a message if successful, or an error if the operation fails.</returns>
    Task<Result<string>> ResetPasswordAsync(string email, string resetCode, string newPassword, CancellationToken cancellationToken = default);
}