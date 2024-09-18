using Ardalis.GuardClauses;
using Microsoft.Extensions.Options;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Identity;
using Microsoft.AspNetCore.Identity;
using Endatix.Infrastructure.Identity;

namespace Endatix.Infrastructure.Auth;

internal sealed class AuthService : IAuthService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly SecuritySettings _securitySettings;

    public AuthService(UserManager<AppUser> userManager, IOptions<SecuritySettings> securitySettings)
    {
        _userManager = userManager;
        _securitySettings = securitySettings.Value;
    }

    public async Task<Result<UserDto>> ValidateCredentials(string email, string password, CancellationToken cancellationToken)
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

        var userDto = new UserDto(email, [], "SystemInfo");

        return Result.Success(userDto);
    }
}
