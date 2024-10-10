using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Microsoft.AspNetCore.Identity;
using Endatix.Infrastructure.Identity;
using Endatix.Core.Entities.Identity;

namespace Endatix.Infrastructure.Auth;

internal sealed class AuthService : IAuthService
{
    private readonly UserManager<AppUser> _userManager;
    private readonly IPasswordHasher<AppUser> _passwordHasher;

    public AuthService(UserManager<AppUser> userManager, IPasswordHasher<AppUser> passwordHasher)
    {
        _userManager = userManager;
        _passwordHasher = passwordHasher;
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

    public async Task<Result<User>> ValidateRefreshToken(long userId, string token, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(userId, nameof(userId));
        Guard.Against.NullOrEmpty(token, nameof(token));

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null || user.RefreshTokenHash is null || user.RefreshTokenExpireAt is null)
        {
            return Result.Invalid(new ValidationError("Invalid input"));
        }

        var verificationResult = _passwordHasher.VerifyHashedPassword(user, user.RefreshTokenHash, token);
        if (verificationResult != PasswordVerificationResult.Success || user.RefreshTokenExpireAt < DateTime.UtcNow)
        {
            return Result.Invalid(new ValidationError("The supplied refresh token is invalid!"));
        }

        return Result.Success(user.ToUserEntity());
    }

    public async Task<Result> StoreRefreshToken(long userId, string token, DateTime expireAt, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(userId, nameof(userId));
        Guard.Against.NullOrEmpty(token, nameof(token));

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Result.Invalid(new ValidationError("Invalid user"));
        }

        var tokenHash = _passwordHasher.HashPassword(user, token);

        user.RefreshTokenHash = tokenHash;
        user.RefreshTokenExpireAt = expireAt;

        await _userManager.UpdateAsync(user);

        return Result.Success();
    }
}
