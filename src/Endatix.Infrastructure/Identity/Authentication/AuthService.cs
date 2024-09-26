using Ardalis.GuardClauses;
using Microsoft.Extensions.Options;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Microsoft.AspNetCore.Identity;
using Endatix.Infrastructure.Identity;
using Endatix.Core.Entities.Identity;

namespace Endatix.Infrastructure.Auth;

internal sealed class AuthService : IAuthService
{
    private readonly UserManager<AppUser> _userManager;

    public AuthService(UserManager<AppUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<Result<User>> ValidateCredentials(string email, string password, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(email, nameof(email));
        Guard.Against.NullOrEmpty(password, nameof(password));

        var user = await _userManager.FindByEmailAsync(email);

        if (user is null || !user.EmailConfirmed)
        {
            return Result.Invalid(new ValidationError("User not found"));
        }

        var userVerified = await _userManager.CheckPasswordAsync(user, password);
        if (!userVerified)
        {
             return Result.Invalid(new ValidationError("The supplied credentials are invalid!"));
        }

        return Result.Success(user.ToUserEntity());
    }
}
