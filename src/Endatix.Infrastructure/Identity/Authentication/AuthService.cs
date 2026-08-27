using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Microsoft.AspNetCore.Identity;
using Endatix.Core.Entities.Identity;

namespace Endatix.Infrastructure.Identity.Authentication;

/// <summary>
/// Provides authentication services based on ASP.NET Core Identity.
/// </summary>  
internal sealed class AuthService(UserManager<AppUser> userManager, IPasswordHasher<AppUser> passwordHasher) : IAuthService
{
    private readonly UserManager<AppUser> _userManager = userManager;
    private readonly IPasswordHasher<AppUser> _passwordHasher = passwordHasher;

    public static readonly string INVALID_CREDENTIALS_ERROR_MESSAGE = "The supplied credentials are invalid";

    /// <summary>
    /// Placeholder used only to burn the same password-hashing work when no account matched.
    /// </summary>
    private static readonly AppUser DummyUser = new() { Email = "dummy@endatix.invalid" };
    private const string DUMMY_PASSWORD = "N0tARea1Passw0rd!";

    /// <summary>
    /// Hash of <see cref="DUMMY_PASSWORD"/>, computed once. A benign race just recomputes an
    /// equivalent hash, so no locking is needed.
    /// </summary>
    private static string? _dummyPasswordHash;

    /// <inheritdoc/>
    public async Task<Result<User>> ValidateCredentials(string email, string password, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(email, nameof(email));
        Guard.Against.NullOrEmpty(password, nameof(password));

        var user = await _userManager.FindByEmailAsync(email);

        // OWASP A07: run the password KDF on every path. Returning early for an unknown or
        // unconfirmed account skips the hash and answers measurably faster, which lets an
        // attacker enumerate accounts by response time even though the payloads are identical.
        var passwordVerified = user is null
            ? BurnPasswordHashingWork(password)
            : await _userManager.CheckPasswordAsync(user, password);

        if (user is null || !user.EmailConfirmed || !passwordVerified)
        {
            return Result.Invalid(new ValidationError(INVALID_CREDENTIALS_ERROR_MESSAGE));
        }

        return Result.Success(user.ToUserEntity());
    }

    /// <summary>
    /// Verifies the supplied password against a throwaway hash so the unknown-account path
    /// costs the same as a real verification. Always returns false.
    /// </summary>
    private bool BurnPasswordHashingWork(string password)
    {
        _dummyPasswordHash ??= _passwordHasher.HashPassword(DummyUser, DUMMY_PASSWORD);
        _passwordHasher.VerifyHashedPassword(DummyUser, _dummyPasswordHash, password);

        return false;
    }

    /// <inheritdoc/>
    public async Task<Result> PersistLoginSessionAsync(
        long userId,
        string refreshToken,
        DateTime refreshTokenExpireAt,
        CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(userId);
        Guard.Against.NullOrEmpty(refreshToken);

        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user is null)
        {
            return Result.Error("Failed to persist login session.");
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        user.RefreshTokenHash = _passwordHasher.HashPassword(user, refreshToken);
        user.RefreshTokenExpireAt = refreshTokenExpireAt;

        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return Result.Error("Failed to persist login session.");
        }

        return Result.Success();
    }

    /// <inheritdoc/>
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

    /// <inheritdoc/>
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
