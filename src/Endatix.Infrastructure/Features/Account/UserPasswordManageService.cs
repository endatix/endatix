using Endatix.Core.Abstractions.Account;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

namespace Endatix.Infrastructure.Features.Account;

public class UserPasswordManageService(
    UserManager<AppUser> userManager) : IUserPasswordManageService
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
                Result.Invalid(new ValidationError("Failed to generate password reset token"));
        }

        return Result.Invalid(new ValidationError("User not found"));
    }
}
