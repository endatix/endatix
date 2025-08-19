using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Account;
using Endatix.Core.Features.Email;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.UseCases.Account.ResetPassword;

public class ResetPasswordHandler(
    IUserPasswordManageService userPasswordManageService,
    IEmailTemplateService emailTemplateService,
    IEmailSender emailSender,
    ILogger<ResetPasswordHandler> logger
) : ICommandHandler<ResetPasswordCommand, Result<string>>
{
    public async Task<Result<string>> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var result = await userPasswordManageService.ResetPasswordAsync(request.Email, request.ResetCode, request.NewPassword, cancellationToken);
        if (result.IsSuccess)
        {
            try
            {
                var passwordChangedEmail = emailTemplateService.CreatePasswordChangedEmail(request.Email);
                await emailSender.SendEmailAsync(passwordChangedEmail, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send password changed email to {Email}", request.Email);
            }

            return Result.Success("Password changed successfully");
        }

        return result;
    }
}
