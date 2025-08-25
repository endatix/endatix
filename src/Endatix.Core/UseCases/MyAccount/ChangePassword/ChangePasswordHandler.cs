using MediatR;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Account;

namespace Endatix.Core.UseCases.MyAccount.ChangePassword;

/// <summary>
/// Handles the change password command
/// </summary>
public class ChangePasswordHandler(IUserPasswordManageService userPasswordManageService) : IRequestHandler<ChangePasswordCommand, Result<string>>
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
            return Result.Success("Password changed successfully");
        }

        return changePasswordResult;
    }
}