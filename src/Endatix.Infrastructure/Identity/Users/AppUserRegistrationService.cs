using Endatix.Core.Abstractions;
using Endatix.Core.Entities.Identity;
using Endatix.Core.Features.Email;
using Endatix.Core.Infrastructure.Result;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Identity.Users;

/// <summary>
/// Implements the user registration service.
/// </summary>
public class AppUserRegistrationService(
    UserManager<AppUser> userManager, 
    IUserStore<AppUser> userStore,
    IEmailVerificationService emailVerificationService,
    IEmailSender emailSender,
    IEmailTemplateService emailTemplateService,
    ILogger<AppUserRegistrationService> logger) : IUserRegistrationService
{
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
        if (!userManager.SupportsUserEmail)
        {
            throw new NotSupportedException($"Registration logic requires a user store with email support. Please check your email settings");
        }

        if (IsDisposableEmail(email))
        {
            return Result.Invalid(new ValidationError("The email is from a suspicious domain."));
        }

        var newUser = new AppUser
        {
            TenantId = 0,           // TODO: This must be 0 for a user that still does not have a tenant. It must be 1 for the initial user seeding so there is a need for a fix in the seeding.
            EmailConfirmed = false  // Users start as unverified and need email verification
        };

        var emailStore = (IUserEmailStore<AppUser>)userStore;
        await userStore.SetUserNameAsync(newUser, email, cancellationToken);
        await emailStore.SetEmailAsync(newUser, email, CancellationToken.None);

        var createUserResult = await userManager.CreateAsync(newUser, password);
        if (!createUserResult.Succeeded)
        {
            if (createUserResult.Errors.Any(error => error.Code == "DuplicateUserName"))
            {
                return Result.Invalid(new ValidationError("The email is already registered."));
            }
            
            var resultErrors = new ErrorList(createUserResult.Errors.Select(error => $"Error code: {error.Code}. {error.Description}"));
            return Result.Error(resultErrors);
        }

        // Create email verification token and send verification email
        var tokenResult = await emailVerificationService.CreateVerificationTokenAsync(newUser.Id, cancellationToken);
        if (tokenResult.IsSuccess)
        {
            try
            {
                var emailModel = emailTemplateService.CreateVerificationEmail(
                    email, 
                    tokenResult.Value!.Token);

                await emailSender.SendEmailAsync(emailModel, cancellationToken);

                logger.LogInformation("Verification email sent successfully to {Email} during registration", email);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send verification email to {Email} during registration", email);
            }
        }
        else
        {
            // Log token creation failure
            logger.LogError("Failed to create verification token for user: {Email} (UserId: {UserId}). Errors: {Errors}", 
                email, newUser.Id, string.Join(", ", tokenResult.ValidationErrors.Select(e => e.ErrorMessage)));
        }

        // If token creation or email sending fails, we should still return success but log the error
        var domainUser = newUser.ToUserEntity();
        return Result.Success(domainUser);
    }

    private bool IsDisposableEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return true;
        }

        var parts = email.Split('@');
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) || string.IsNullOrWhiteSpace(parts[1]))
        {
            return true;
        }

        var domain = parts[1].Trim().ToLowerInvariant();
        return _disposableDomains.Contains(domain);
    }
}
