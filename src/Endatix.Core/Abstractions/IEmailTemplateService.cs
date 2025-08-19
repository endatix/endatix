using Endatix.Core.Features.Email;

namespace Endatix.Core.Abstractions;

/// <summary>
/// Defines the contract for email template operations.
/// </summary>
public interface IEmailTemplateService
{
    /// <summary>
    /// Creates a verification email template for the specified user and token.
    /// </summary>
    /// <param name="userEmail">The email address of the user.</param>
    /// <param name="token">The verification token.</param>
    /// <returns>An EmailWithTemplate configured for verification.</returns>
    EmailWithTemplate CreateVerificationEmail(string userEmail, string token);

    /// <summary>
    /// Creates a reset password email template for the specified user and token.
    /// </summary>
    /// <param name="userEmail">The email address of the user.</param>
    /// <param name="token">The reset password token.</param>
    /// <returns>An EmailWithTemplate configured for reset password.</returns>
    EmailWithTemplate CreateForgotPasswordEmail(string userEmail, string token);

    /// <summary>
    /// Creates a password changed email template for the specified user.
    /// </summary>
    /// <param name="userEmail">The email address of the user.</param>
    /// <returns>An EmailWithTemplate configured for password changed.</returns>
    EmailWithTemplate CreatePasswordChangedEmail(string userEmail);
}