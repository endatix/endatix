using Endatix.Core.Abstractions.Account;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Account.ResetPassword;

public class ResetPasswordHandler(
    IUserPasswordManageService userPasswordManageService
) : ICommandHandler<ResetPasswordCommand, Result<string>>
{
    public async Task<Result<string>> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var result = await userPasswordManageService.ResetPasswordAsync(request.Email, request.ResetCode, request.NewPassword, cancellationToken);
        if (result.IsSuccess)
        {
            return Result.Success("Password reset successfully");
        }

        return result;
    }
}
