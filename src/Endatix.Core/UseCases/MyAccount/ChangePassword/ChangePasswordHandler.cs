using MediatR;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Account;
using Endatix.Core.Features.Email;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.UseCases.MyAccount.ChangePassword;

/// <summary>
/// Handles the change password command
/// </summary>
public class ChangePasswordHandler(
    IUserPasswordManageService userPasswordManageService,
    IEmailTemplateService emailTemplateService,
    IEmailSender emailSender,
    ILogger<ChangePasswordHandler> logger
    ) : IRequestHandler<ChangePasswordCommand, Result<string>>
{
    /// <summary>
    /// Handles the password change request
    /// </summary>
    /// <param name="request">The change password command</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    public async Task<Result<string>> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId is null || request.UserId.Value <= 0)
        {
            return Result.Invalid(new ValidationError("User not found"));
        }

        var changePasswordResult = await userPasswordManageService.ChangePasswordAsync(request.UserId.Value, request.CurrentPassword, request.NewPassword, cancellationToken);

        if (changePasswordResult.IsSuccess)
        {
            try
            {
                var passwordChangedEmail = emailTemplateService.CreatePasswordChangedEmail(changePasswordResult.Value.Email);
                await emailSender.SendEmailAsync(passwordChangedEmail, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to send password changed email to {Email}", changePasswordResult.Value.Email);
            }
            return Result.Success("Password changed successfully");
        }

        return changePasswordResult.Status switch
        {
            ResultStatus.Invalid => Result.Invalid(changePasswordResult.ValidationErrors),
            ResultStatus.Error => Result.Error(new ErrorList(changePasswordResult.Errors, changePasswordResult.CorrelationId)),
            _ => Result.Error("An unexpected error occurred")
        };
    }
}