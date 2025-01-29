using System.Security.Claims;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Microsoft.AspNetCore.Identity;

namespace Endatix.Infrastructure.Identity.Users;

/// <summary>
/// Implements the user service by leveraging the integration with ASP.NET Core Identity including the registered <see cref="AppUser" /> persisted object />
/// </summary>
public class AppUserService(UserManager<AppUser> userManager) : IUserService
{
    /// <inheritdoc />
    public async Task<Result<User>> GetUserAsync(ClaimsPrincipal claimsPrincipal, CancellationToken cancellationToken = default)
    {
        if (claimsPrincipal == null)
        {
            return Result.NotFound();
        }

        var user = await userManager.GetUserAsync(claimsPrincipal);
        if (user == null)
        {
            return Result.NotFound();
        }

        return Result.Success(user.ToUserEntity());
    }

    public async Task<Result<string>> ChangePasswordAsync(User user, string currentPassword, string newPassword, CancellationToken cancellationToken = default)
    {
        var appUser = await userManager.FindByIdAsync(user.Id.ToString());
        if (appUser == null)
        {
            return Result<string>.Error("User not found");
        }

        if (string.IsNullOrEmpty(currentPassword))
        {
            return Result<string>.Error("The current password is required to set a new password. If the old password is forgotten, use password reset.");
        }

        try
        {
            var changePasswordResult = await userManager.ChangePasswordAsync(appUser, currentPassword, newPassword);
            if (!changePasswordResult.Succeeded)
            {
                return Result.Error(string.Join(", ", changePasswordResult.Errors.Select(e => e.Description)));
            }
        }
        catch (Exception ex)
        {
            return Result.Error(ex.Message);
        }

        return Result.Success("Password changed successfully");
    }
}
