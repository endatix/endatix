using System.Security.Cryptography;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Specifications;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Endatix.Infrastructure.Identity.EmailVerification;

/// <summary>
/// Implements the email verification service.
/// </summary>
public class EmailVerificationService : IEmailVerificationService
{
    private const int TOKEN_SIZE_BYTES = 32; // 256 bits
    private readonly IRepository<EmailVerificationToken> _tokenRepository;
    private readonly UserManager<AppUser> _userManager;
    private readonly EmailVerificationOptions _options;

    public EmailVerificationService(
        IRepository<EmailVerificationToken> tokenRepository,
        UserManager<AppUser> userManager,
        IOptions<EmailVerificationOptions> options)
    {
        Guard.Against.Null(tokenRepository);
        Guard.Against.Null(userManager);
        Guard.Against.Null(options.Value);
        Guard.Against.NegativeOrZero(options.Value.TokenExpiryInHours);

        _tokenRepository = tokenRepository;
        _userManager = userManager;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task<Result<EmailVerificationToken>> CreateVerificationTokenAsync(long userId, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(userId);

        // Check if user exists and is not already verified
        var user = await _userManager.FindByIdAsync(userId.ToString());
        if (user == null)
        {
            return Result.NotFound("User not found");
        }

        if (user.EmailConfirmed)
        {
            return Result.Invalid(new ValidationError("User is already verified"));
        }

        // Delete any existing tokens for this user
        var existingTokens = await _tokenRepository.ListAsync(
            new EmailVerificationTokenByUserIdSpec(userId), 
            cancellationToken);
        
        foreach (var existingToken in existingTokens)
        {
            await _tokenRepository.DeleteAsync(existingToken, cancellationToken);
        }

        // Create new token
        var tokenValue = GenerateToken();
        var expiresAt = DateTime.UtcNow.AddHours(_options.TokenExpiryInHours);
        var verificationToken = new EmailVerificationToken(userId, tokenValue, expiresAt);

        await _tokenRepository.AddAsync(verificationToken, cancellationToken);
        await _tokenRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(verificationToken);
    }

    /// <inheritdoc />
    public async Task<Result<User>> VerifyEmailAsync(string token, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrEmpty(token);

        var verificationToken = await _tokenRepository.FirstOrDefaultAsync(
            new EmailVerificationTokenByTokenSpec(token), 
            cancellationToken);

        if (verificationToken == null)
        {
            return Result.NotFound("Invalid verification token");
        }

        if (verificationToken.IsExpired)
        {
            return Result.Invalid(new ValidationError("Verification token has expired"));
        }

        if (verificationToken.IsUsed)
        {
            return Result.Invalid(new ValidationError("Verification token has already been used"));
        }

        var user = await _userManager.FindByIdAsync(verificationToken.UserId.ToString());
        if (user == null)
        {
            return Result.NotFound("User not found");
        }

        if (user.EmailConfirmed)
        {
            return Result.Invalid(new ValidationError("User is already verified"));
        }

        // Mark user as verified
        user.EmailConfirmed = true;
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return Result.Error("Failed to verify user");
        }

        // Mark token as used
        verificationToken.MarkAsUsed();
        await _tokenRepository.UpdateAsync(verificationToken, cancellationToken);
        await _tokenRepository.SaveChangesAsync(cancellationToken);

        return Result.Success(user.ToUserEntity());
    }

    private string GenerateToken()
    {
        var tokenBytes = new byte[TOKEN_SIZE_BYTES];
        RandomNumberGenerator.Fill(tokenBytes);
        return Convert.ToHexString(tokenBytes);
    }
} 