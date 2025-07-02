using Endatix.Core.Abstractions;
using Endatix.Core.Features.Email;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.UseCases.Identity.SendVerificationEmail;

/// <summary>
/// Handles the sending of verification emails to users.
/// </summary>
public class SendVerificationEmailHandler(
    IEmailVerificationService emailVerificationService,
    IUserService userService,
    IEmailSender emailSender,
    IEmailTemplateService emailTemplateService,
    ILogger<SendVerificationEmailHandler> logger) : ICommandHandler<SendVerificationEmailCommand, Result>
{
    /// <summary>
    /// Handles the SendVerificationEmailCommand to send a verification email.
    /// </summary>
    /// <param name="request">The SendVerificationEmailCommand containing the email address.</param>
    /// <param name="cancellationToken">A cancellation token that can be used to cancel the work.</param>
    /// <returns>A Result containing the operation status.</returns>
    public async Task<Result> Handle(SendVerificationEmailCommand request, CancellationToken cancellationToken)
    {
        var userResult = await userService.GetUserAsync(request.Email, cancellationToken);
        if (!userResult.IsSuccess)
        {
            // Log the attempt to send verification email to non-existent user
            logger.LogWarning("Verification email requested for non-existent user: {Email}", request.Email);
            
            // Return success to prevent email enumeration attacks
            return Result.Success();
        }

        var user = userResult.Value!;
        if (user.IsVerified)
        {
            // Log the attempt to send verification email to already verified user
            logger.LogWarning("Verification email requested for already verified user: {Email} (UserId: {UserId})",
                request.Email, user.Id);
            
            // Return success to prevent email enumeration attacks
            return Result.Success();
        }

        var tokenResult = await emailVerificationService.CreateVerificationTokenAsync(user.Id, cancellationToken);
        if (tokenResult.IsSuccess)
        {
            try
            {
                var emailModel = emailTemplateService.CreateVerificationEmail(
                    user.Email, 
                    tokenResult.Value!.Token);

                await emailSender.SendEmailAsync(emailModel, cancellationToken);

                logger.LogInformation("Verification email sent successfully to {Email}", user.Email);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send verification email to {Email}", user.Email);
            }
        }
        else
        {
            // Log token creation failure
            logger.LogError("Failed to create verification token for user: {Email} (UserId: {UserId}). Errors: {Errors}", 
                request.Email, user.Id, string.Join(", ", tokenResult.ValidationErrors.Select(e => e.ErrorMessage)));
        }

        // If token creation or email sending fails, we should still return success but log the error
        return Result.Success();
    }
} 