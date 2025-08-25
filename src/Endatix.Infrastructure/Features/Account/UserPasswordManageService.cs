using System.Runtime.CompilerServices;
using System.Text;
using Endatix.Core.Abstractions.Account;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.Account;

public class UserPasswordManageService(
    UserManager<AppUser> userManager,
    ILogger<UserPasswordManageService> logger
    ) : IUserPasswordManageService
{
    /// <inheritdoc />
    public async Task<Result<string>> GeneratePasswordResetTokenAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Result.Invalid(new ValidationError("Email is required"));
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is not null && await userManager.IsEmailConfirmedAsync(user))
        {
            var token = await userManager.GeneratePasswordResetTokenAsync(user);
            return token is not null ?
                Result.Success(token) :
                Result.Error("Failed to generate password reset token");
        }

        return Result.Invalid(new ValidationError("User not found"));
    }

    /// <inheritdoc />
    public async Task<Result<string>> ResetPasswordAsync(string email, string resetCode, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(resetCode) || string.IsNullOrWhiteSpace(newPassword))
        {
            return Result.Invalid(new ValidationError("Invalid input"));
        }

        var user = await userManager.FindByEmailAsync(email);
        if (user is null || !await userManager.IsEmailConfirmedAsync(user))
        {
            logger.LogWarning("User not found or email not confirmed for email: {Email}", email);
            return Result.Invalid(new ValidationError("Invalid input")); // Don't leak information about the user's existence
        }

        IdentityResult result;
        try
        {
            var code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(resetCode));
            result = await userManager.ResetPasswordAsync(user, code, newPassword);
        }
        catch (FormatException)
        {
            result = IdentityResult.Failed();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An unexpected error occurred while resetting the password");
            return Result.Error("Could not reset password. Please try again or contact support.");
        }

        if (!result.Succeeded)
        {
            // For as many cases as possible, return consistent error with the error code invalid_token to prevent email enumeration attacks
            return Result.Invalid(new ValidationError(
                errorMessage: "Invalid reset code or token expired",
                errorCode: "invalid_token",
                severity: ValidationSeverity.Error,
                identifier: "reset_password"
                ));
        }

        return Result.Success("Password reset successfully");
    }

    /// <inheritdoc />
    public async Task<Result<string>> ChangePasswordAsync(long userId, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(currentPassword))
        {
            return Result.Invalid(new ValidationError("The current password is required to set a new password. If the old password is forgotten, use password reset."));
        }

        if (string.IsNullOrEmpty(newPassword))
        {
            return Result.Invalid(new ValidationError("The new password is required to change the password."));
        }

        var appUser = await userManager.FindByIdAsync(userId.ToString());
        if (appUser == null)
        {
            return Result.Invalid(new ValidationError("User not found"));
        }

        try
        {
            var changePasswordResult = await userManager.ChangePasswordAsync(appUser, currentPassword, newPassword);
            if (!changePasswordResult.Succeeded)
            {
                return Result.Invalid(changePasswordResult.Errors.Select(e => new ValidationError(e.Description)
                {
                    Identifier = "change_password",
                    ErrorCode = e.Code,
                    Severity = ValidationSeverity.Error
                }));
            }
        }
        catch (Exception ex)
        {
            return Result.Error(ex.Message);
        }

        return Result.Success("Password changed successfully");
    }
}
