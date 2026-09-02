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

    /// <summary>
    /// Single client-facing message for every unresolvable verification token, so a caller
    /// cannot tell an unknown token apart from one whose account no longer exists.
    /// </summary>
    public const string INVALID_VERIFICATION_TOKEN_MESSAGE = "Invalid verification token";

    /// <summary>
    /// Single client-facing message for every unresolvable invite token, for the same reason.
    /// </summary>
    public const string INVALID_INVITE_TOKEN_MESSAGE = "Invalid invite token";
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

        if (existingTokens.Count > 0)
        {
            await _tokenRepository.DeleteRangeAsync(existingTokens, cancellationToken);
        }

        // Create new token
        var tokenValue = GenerateToken();
        var expiresAt = DateTime.UtcNow.AddHours(_options.TokenExpiryInHours);
        var verificationToken = new EmailVerificationToken(userId, tokenValue, expiresAt);

        await _tokenRepository.AddAsync(verificationToken, cancellationToken);

        return Result.Success(verificationToken);
    }

    /// <inheritdoc />
    public async Task<Result<User>> VerifyEmailAsync(string token, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(token);

        var verificationToken = await _tokenRepository.FirstOrDefaultAsync(
            new EmailVerificationTokenByTokenSpec(token),
            cancellationToken);

        if (verificationToken == null)
        {
            // Same status and message as the expired/used branches below: the response must not
            // tell the caller whether the token ever existed.
            return Result.Invalid(new ValidationError(INVALID_VERIFICATION_TOKEN_MESSAGE));
        }

        var user = await _userManager.FindByIdAsync(verificationToken.UserId.ToString());
        if (user == null)
        {
            // Report the same failure as an unknown token. A dangling token must not tell the
            // caller that the token itself was genuine but the account behind it is gone.
            return Result.Invalid(new ValidationError(INVALID_VERIFICATION_TOKEN_MESSAGE));
        }

        // Re-clicks, Hub Strict-Mode double POST, and "already verified" must succeed. Hub maps
        // "User is already verified" to a hard error; a used token after a race is not a failure.
        if (user.EmailConfirmed)
        {
            if (!verificationToken.IsUsed)
            {
                verificationToken.MarkAsUsed();
                await _tokenRepository.UpdateAsync(verificationToken, cancellationToken);
            }

            return Result.Success(user.ToUserEntity());
        }

        if (verificationToken.IsExpired)
        {
            return Result.Invalid(new ValidationError("Verification token has expired"));
        }

        if (verificationToken.IsUsed)
        {
            return Result.Invalid(new ValidationError("Verification token has already been used"));
        }

        user.EmailConfirmed = true;
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return Result.Error("Failed to verify user");
        }

        verificationToken.MarkAsUsed();
        await _tokenRepository.UpdateAsync(verificationToken, cancellationToken);

        return Result.Success(user.ToUserEntity());
    }

    /// <inheritdoc />
    public async Task<Result<User>> ActivateInviteAsync(string token, string newPassword, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(token);
        Guard.Against.NullOrWhiteSpace(newPassword);

        var inviteResult = await ResolvePendingInviteAsync(token, cancellationToken);
        if (!inviteResult.IsSuccess || inviteResult.Value is null)
        {
            return inviteResult.ToErrorResult<User>();
        }

        return await ActivatePendingInviteAsync(inviteResult.Value, newPassword, cancellationToken);
    }

    private async Task<Result<User>> ActivatePendingInviteAsync(
        PendingInvite invite,
        string newPassword,
        CancellationToken cancellationToken)
    {
        var verificationToken = invite.Token;
        var user = invite.User;
        var resetToken = await _userManager.GeneratePasswordResetTokenAsync(user);
        var resetResult = await _userManager.ResetPasswordAsync(user, resetToken, newPassword);
        if (!resetResult.Succeeded)
        {
            return Result.Invalid(resetResult.Errors.Select(error => new ValidationError
            {
                Identifier = error.Code,
                ErrorMessage = error.Description
            }));
        }

        user.EmailConfirmed = true;
        var updateResult = await _userManager.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            return Result.Error(new ErrorList(updateResult.Errors.Select(error => error.Description)));
        }

        verificationToken.MarkAsUsed();
        await _tokenRepository.UpdateAsync(verificationToken, cancellationToken);

        return Result.Success(user.ToUserEntity());
    }

    /// <inheritdoc />
    public async Task<Result<User>> GetPendingInviteUserAsync(string token, CancellationToken cancellationToken)
    {
        Guard.Against.NullOrWhiteSpace(token);

        var inviteResult = await ResolvePendingInviteAsync(token, cancellationToken);
        return inviteResult.IsSuccess && inviteResult.Value is not null
            ? Result.Success(inviteResult.Value.User.ToUserEntity())
            : inviteResult.ToErrorResult<User>();
    }

    /// <inheritdoc />
    public async Task<Result> InvalidateVerificationTokensAsync(long userId, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(userId);

        var tokens = await _tokenRepository.ListAsync(
            new EmailVerificationTokenByUserIdSpec(userId),
            cancellationToken);
        var unusedTokens = tokens
            .Where(token => !token.IsUsed)
            .ToList();

        if (unusedTokens.Count == 0)
        {
            return Result.Success();
        }

        foreach (var token in unusedTokens)
        {
            token.MarkAsUsed();
        }

        await _tokenRepository.UpdateRangeAsync(unusedTokens, cancellationToken);
        return Result.Success();
    }

    private string GenerateToken()
    {
        var tokenBytes = new byte[TOKEN_SIZE_BYTES];
        RandomNumberGenerator.Fill(tokenBytes);
        return Convert.ToHexString(tokenBytes);
    }

    private async Task<Result<PendingInvite>> ResolvePendingInviteAsync(string token, CancellationToken cancellationToken)
    {
        var verificationToken = await _tokenRepository.FirstOrDefaultAsync(
            new EmailVerificationTokenByTokenSpec(token),
            cancellationToken);

        if (verificationToken == null)
        {
            return Result.Invalid(new ValidationError(INVALID_INVITE_TOKEN_MESSAGE));
        }

        if (verificationToken.IsExpired)
        {
            return Result.Invalid(new ValidationError("Invite token has expired"));
        }

        if (verificationToken.IsUsed)
        {
            return Result.Invalid(new ValidationError("Invite token has already been used"));
        }

        var user = await _userManager.FindByIdAsync(verificationToken.UserId.ToString());
        if (user == null)
        {
            return Result.Invalid(new ValidationError(INVALID_INVITE_TOKEN_MESSAGE));
        }

        if (user.EmailConfirmed)
        {
            return Result.Invalid(new ValidationError("Invite has already been activated"));
        }

        return Result.Success(new PendingInvite(verificationToken, user));
    }

    private sealed record PendingInvite(EmailVerificationToken Token, AppUser User);
}