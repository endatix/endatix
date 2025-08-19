using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Account;
using Endatix.Core.Features.Email;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.UseCases.Account.ForgotPassword;

public class ForgotPasswordHandler(
    IUserPasswordManageService userPasswordManageService,
    IEmailTemplateService emailTemplateService,
    IEmailSender emailSender,
    ILogger<ForgotPasswordHandler> logger
) : ICommandHandler<ForgotPasswordCommand, Result<string>>
{
    public const string GENERAL_SUCCESS_MESSAGE = "Thank you. If an account exists with this email, you will receive an email with instructions to reset your password.";
    public const string FAILED_TO_SEND_EMAIL_MESSAGE = "Failed to send forgot password email. Please contact support to assist you.";

    public async Task<Result<string>> Handle(ForgotPasswordCommand request, CancellationToken cancellationToken)
    {

        var tokenResult = await userPasswordManageService.GeneratePasswordResetTokenAsync(request.Email, cancellationToken);
        if (tokenResult.IsSuccess)
        {
            try
            {
                var email = emailTemplateService.CreateForgotPasswordEmail(request.Email, tokenResult.Value);
                await emailSender.SendEmailAsync(email, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send forgot password email to {Email}", request.Email);
                return Result.Error(FAILED_TO_SEND_EMAIL_MESSAGE);
            }
        }

        return Result.Success(GENERAL_SUCCESS_MESSAGE); // except for server errors, always return success message to prevent email enumeration attacks
    }
}