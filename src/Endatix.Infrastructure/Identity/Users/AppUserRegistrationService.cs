using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Result;
using Microsoft.AspNetCore.Identity;

namespace Endatix.Infrastructure.Identity.Users;

/// <summary>
/// Implements the user registration service.
/// </summary>
public class AppUserRegistrationService(UserManager<AppUser> userManager, IUserStore<AppUser> userStore) : IUserRegistrationService
{
    /// <inheritdoc />
    public async Task<Result<User>> RegisterUserAsync(long tenantId, string email, string password, CancellationToken cancellationToken)
    {
        if (!userManager.SupportsUserEmail)
        {
            throw new NotSupportedException($"Registration logic requires a user store with email support. Please check your email settings");
        }

        var newUser = new AppUser
        {
            TenantId = tenantId,
            EmailConfirmed = true
        };

        var emailStore = (IUserEmailStore<AppUser>)userStore;
        await userStore.SetUserNameAsync(newUser, email, cancellationToken);
        await emailStore.SetEmailAsync(newUser, email, CancellationToken.None);

        var createUserResult = await userManager.CreateAsync(newUser, password);

        if (!createUserResult.Succeeded)
        {
            var resultErrors = new ErrorList(createUserResult.Errors.Select(error => $"Error code: {error.Code}. {error.Description}"));
            return Result.Error(resultErrors);
        }

        var domainUser = newUser.ToUserEntity();
        return Result.Success(domainUser);
    }
}