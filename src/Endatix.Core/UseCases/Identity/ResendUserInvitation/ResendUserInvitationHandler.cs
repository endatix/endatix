using Endatix.Core.Abstractions;
using Endatix.Core.Features.Email;
using Endatix.Core.Infrastructure.Logging;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.UseCases.Identity.ResendUserInvitation;

/// <summary>
/// Handler for the <see cref="ResendUserInvitationCommand"/> class.
/// </summary>
public sealed class ResendUserInvitationHandler(
    IUserService userService,
    ITenantContext tenantContext,
    IEmailVerificationService emailVerificationService,
    IEmailSender emailSender,
    IEmailTemplateService emailTemplateService,
    ILogger<ResendUserInvitationHandler> logger) : ICommandHandler<ResendUserInvitationCommand, Result>
{
    /// <inheritdoc/>
    public async Task<Result> Handle(ResendUserInvitationCommand request, CancellationToken cancellationToken)
    {
        var userResult = await userService.GetUserAsync(request.UserId, cancellationToken);
        if (!userResult.IsSuccess || userResult.Value is null)
        {
            return Result.NotFound("User not found.");
        }

        if (userResult.Value.TenantId != tenantContext.TenantId)
        {
            return Result.NotFound("User not found for the current tenant.");
        }

        if (userResult.Value.IsVerified)
        {
            return Result.Invalid(new ValidationError("The user has already verified their email."));
        }

        var tokenResult = await emailVerificationService.CreateVerificationTokenAsync(userResult.Value.Id, cancellationToken);
        if (!tokenResult.IsSuccess || tokenResult.Value is null)
        {
            return tokenResult.Status switch
            {
                ResultStatus.NotFound => Result.NotFound(tokenResult.Errors.ToArray()),
                ResultStatus.Invalid => Result.Invalid(tokenResult.ValidationErrors),
                ResultStatus.Forbidden => Result.Forbidden(tokenResult.Errors.ToArray()),
                ResultStatus.Unauthorized => Result.Unauthorized(tokenResult.Errors.ToArray()),
                _ => Result.Error(new ErrorList(tokenResult.Errors))
            };
        }

        try
        {
            var emailModel = emailTemplateService.CreateInvitationEmail(
                userResult.Value.Email,
                tokenResult.Value.RawToken!);

            await emailSender.SendEmailAsync(emailModel, cancellationToken);
            logger.LogInformation(
                "Invitation email resent successfully to {Email}",
                PiiRedactor.RedactEmail());

            return Result.Success();
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to resend invitation email to {Email}",
                PiiRedactor.RedactEmail());

            return Result.Error("Failed to resend invitation email.");
        }
    }
}
