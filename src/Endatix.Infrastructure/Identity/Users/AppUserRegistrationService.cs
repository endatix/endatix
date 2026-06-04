using System.Security.Cryptography;
using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Features.Email;
using Endatix.Core.Infrastructure.Logging;
using Endatix.Core.Infrastructure.Result;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Identity.Users;

/// <summary>
/// Implements the user registration service.
/// </summary>
public sealed class AppUserRegistrationService(
    UserManager<AppUser> userManager,
    IUserStore<AppUser> userStore,
    IEmailVerificationService emailVerificationService,
    IEmailSender emailSender,
    IEmailTemplateService emailTemplateService,
    ILogger<AppUserRegistrationService> logger) : IUserRegistrationService
{
    private const string EmailAlreadyRegisteredMessage = "The email is already registered.";
    private const string SuspiciousEmailMessage = "The email is from a suspicious domain.";
    private const string UserAlreadyBelongsToTenantMessage = "The user already belongs to this tenant.";
    private const string UppercaseChars = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string LowercaseChars = "abcdefghijkmnopqrstuvwxyz";
    private const string DigitChars = "23456789";
    private const string SpecialChars = "!@$?_#*-+";
    private const int TemporaryInvitePasswordLength = 24;

    // This is a short list of the top domains known to be used most frequently in disposable email registrations.
    // It is a good start but for better results more complete lists should be used, like from e.g. https://github.com/disposable-email-domains/disposable-email-domains
    // An even better approach is to use more advanced email validation techniques than just using blocked domains lists.
    private static readonly HashSet<string> _disposableDomains = new(StringComparer.OrdinalIgnoreCase)
    {
        "mailinator.com",
        "10minutemail.com",
        "tempmail.com",
        "guerrillamail.com",
        "yopmail.com",
        "trashmail.com",
        "getnada.com",
        "emailondeck.com",
        "fakeinbox.com",
        "dispostable.com",
        "throwawaymail.com",
        "maildrop.cc",
        "spamgourmet.com",
        "mintemail.com",
        "mytemp.email",
        "moakt.com",
        "mailcatch.com",
        "inboxkitten.com",
        "temp-mail.org",
        "easytrashmail.com"
    };

    /// <inheritdoc />
    public async Task<Result<User>> RegisterUserAsync(string email, string password, CancellationToken cancellationToken)
    {
        return await RegisterUserAsync(email, password, tenantId: 0, isEmailConfirmed: false, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<User>> RegisterUserAsync(string email, string password, long tenantId, bool isEmailConfirmed, CancellationToken cancellationToken)
    {
        return await RegisterUserAsync(
            email,
            password,
            tenantId,
            isEmailConfirmed,
            sendInvitationEmail: false,
            cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Result<User>> RegisterInvitedUserAsync(string email, long tenantId, CancellationToken cancellationToken)
    {
        return await RegisterUserAsync(
            email,
            GenerateTemporaryInvitePassword(),
            tenantId,
            isEmailConfirmed: false,
            sendInvitationEmail: true,
            cancellationToken);
    }

    private async Task<Result<User>> RegisterUserAsync(
        string email,
        string password,
        long tenantId,
        bool isEmailConfirmed,
        bool sendInvitationEmail,
        CancellationToken cancellationToken)
    {
        if (!userManager.SupportsUserEmail)
        {
            throw new NotSupportedException($"Registration logic requires a user store with email support. Please check your email settings");
        }

        var emailGuard = ValidateRegistrationEmail(email);
        if (!emailGuard.IsSuccess)
        {
            return emailGuard.ToErrorResult<User>();
        }

        var normalizedEmail = email.Trim();
        var existingUser = await userManager.FindByEmailAsync(normalizedEmail);
        if (existingUser is not null)
        {
            return await HandleExistingUserAsync(existingUser, normalizedEmail, tenantId, sendInvitationEmail, cancellationToken);
        }

        var newUser = new AppUser
        {
            TenantId = tenantId,
            EmailConfirmed = isEmailConfirmed
        };

        var emailStore = (IUserEmailStore<AppUser>)userStore;
        await userStore.SetUserNameAsync(newUser, normalizedEmail, cancellationToken);
        await emailStore.SetEmailAsync(newUser, normalizedEmail, cancellationToken);

        var createUserResult = await userManager.CreateAsync(newUser, password);
        if (!createUserResult.Succeeded)
        {
            if (createUserResult.Errors.Any(error => error.Code == "DuplicateUserName"))
            {
                return Result.Invalid(new ValidationError(EmailAlreadyRegisteredMessage));
            }

            return ToIdentityErrorResult(createUserResult);
        }

        // Create email verification token and send verification email if email is not already confirmed
        if (!isEmailConfirmed)
        {
            await SendAccountEmailAsync(newUser.Id, normalizedEmail, sendInvitationEmail, cancellationToken);
        }
        else
        {
            logger.LogInformation("Skipping email verification for {Email} - email is already confirmed", RedactEmail(normalizedEmail));
        }

        // If token creation or email sending fails, we should still return success but log the error
        var domainUser = newUser.ToUserEntity();
        return Result.Success(domainUser);
    }

    private async Task<Result<User>> HandleExistingUserAsync(
        AppUser existingUser,
        string email,
        long tenantId,
        bool sendInvitationEmail,
        CancellationToken cancellationToken)
    {
        if (existingUser.TenantId > 0 && existingUser.TenantId != tenantId)
        {
            return Result.Invalid(new ValidationError(EmailAlreadyRegisteredMessage));
        }

        if (existingUser.TenantId != tenantId)
        {
            return await AttachExistingUserToTenantAsync(existingUser, email, tenantId, sendInvitationEmail, cancellationToken);
        }

        if (existingUser.EmailConfirmed)
        {
            return Result.Invalid(new ValidationError(UserAlreadyBelongsToTenantMessage));
        }

        await SendAccountEmailAsync(existingUser.Id, email, sendInvitationEmail, cancellationToken);
        return Result.Success(existingUser.ToUserEntity());
    }

    private async Task<Result<User>> AttachExistingUserToTenantAsync(
        AppUser existingUser,
        string email,
        long tenantId,
        bool sendInvitationEmail,
        CancellationToken cancellationToken)
    {
        existingUser.TenantId = tenantId;
        var updateResult = await userManager.UpdateAsync(existingUser);
        if (!updateResult.Succeeded)
        {
            return Result.Error(new ErrorList(updateResult.Errors.Select(error => $"Error code: {error.Code}. {error.Description}")));
        }

        if (!existingUser.EmailConfirmed)
        {
            await SendAccountEmailAsync(existingUser.Id, email, sendInvitationEmail, cancellationToken);
        }

        return Result.Success(existingUser.ToUserEntity());
    }

    private async Task SendAccountEmailAsync(
        long userId,
        string email,
        bool sendInvitationEmail,
        CancellationToken cancellationToken)
    {
        var tokenResult = await emailVerificationService.CreateVerificationTokenAsync(userId, cancellationToken);
        if (!tokenResult.IsSuccess)
        {
            LogVerificationTokenFailure(userId, email, tokenResult);
            return;
        }

        var rawToken = tokenResult.Value?.RawToken;
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            logger.LogError("Failed to send account email to {Email} during registration because the verification token is missing.", RedactEmail(email));
            return;
        }

        try
        {
            var emailModel = sendInvitationEmail
                ? emailTemplateService.CreateInvitationEmail(email, rawToken)
                : emailTemplateService.CreateVerificationEmail(email, rawToken);

            await emailSender.SendEmailAsync(emailModel, cancellationToken);

            logger.LogInformation("{EmailKind} email sent successfully to {Email} during registration",
                sendInvitationEmail ? "Invitation" : "Verification",
                RedactEmail(email));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send account email to {Email} during registration", RedactEmail(email));
        }
    }

    private static Result ValidateRegistrationEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return SuspiciousEmail();
        }

        var trimmedEmail = email.Trim();
        var atIndex = trimmedEmail.IndexOf('@');
        if (atIndex <= 0 || atIndex != trimmedEmail.LastIndexOf('@') || atIndex == trimmedEmail.Length - 1)
        {
            return SuspiciousEmail();
        }

        var domain = trimmedEmail[(atIndex + 1)..].Trim();
        return _disposableDomains.Contains(domain)
            ? SuspiciousEmail()
            : Result.Success();
    }

    private static Result<User> ToIdentityErrorResult(IdentityResult identityResult)
    {
        var resultErrors = new ErrorList(identityResult.Errors.Select(error => $"Error code: {error.Code}. {error.Description}"));
        return Result.Error(resultErrors);
    }

    private static Result SuspiciousEmail()
    {
        return Result.Invalid(new ValidationError(SuspiciousEmailMessage));
    }

    private static string GenerateTemporaryInvitePassword()
    {
        var allChars = (UppercaseChars + LowercaseChars + DigitChars + SpecialChars).ToCharArray();
        var password = new char[TemporaryInvitePasswordLength];

        password[0] = GetRandomChar(UppercaseChars);
        password[1] = GetRandomChar(LowercaseChars);
        password[2] = GetRandomChar(DigitChars);
        password[3] = GetRandomChar(SpecialChars);

        for (var i = 4; i < password.Length; i++)
        {
            password[i] = allChars[RandomNumberGenerator.GetInt32(allChars.Length)];
        }

        Shuffle(password);
        return new string(password);
    }

    private static char GetRandomChar(string charset)
    {
        return charset[RandomNumberGenerator.GetInt32(charset.Length)];
    }

    private static void Shuffle(Span<char> chars)
    {
        for (var i = chars.Length - 1; i > 0; i--)
        {
            var swapIndex = RandomNumberGenerator.GetInt32(i + 1);
            (chars[i], chars[swapIndex]) = (chars[swapIndex], chars[i]);
        }
    }

    private void LogVerificationTokenFailure(long userId, string email, Result<EmailVerificationToken> tokenResult)
    {
        var errors = tokenResult.ValidationErrors.Any()
            ? tokenResult.ValidationErrors.Select(error => error.ErrorMessage)
            : tokenResult.Errors;

        logger.LogError("Failed to create verification token for user: {Email} (UserId: {UserId}). Errors: {Errors}",
            RedactEmail(email), userId, string.Join(", ", errors));
    }

    private static string RedactEmail(string email)
    {
        return PiiRedactor.Redact(email, SensitivityType.Email);
    }
}
