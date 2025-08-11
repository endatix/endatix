using MediatR;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.Abstractions;

namespace Endatix.Core.UseCases.MyAccount.ChangePassword;

/// <summary>
/// Handles the change password command
/// </summary>
public class ChangePasswordHandler(IUserService userService) : IRequestHandler<ChangePasswordCommand, Result<string>>
{
    /// <summary>
    /// Handles the password change request
    /// </summary>
    /// <param name="request">The change password command</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Result indicating success or failure</returns>
    public async Task<Result<string>> Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        if (request.UserId == null)
        {
            return Result.Invalid(new ValidationError("User not found"));
        }

        var userResult = await userService.GetUserAsync(request.UserId.Value, cancellationToken);

        if (!userResult.IsSuccess || userResult.Value is not { } user)
        {
            return Result.Invalid(new ValidationError("User not found"));
        }

        var changePasswordResult = await userService.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword, cancellationToken);

        if (changePasswordResult.IsSuccess)
        {
            return Result.Success("Password changed successfully");
        }

        return Result.Invalid(new ValidationError(changePasswordResult.Errors?.First() ?? "Failed to change password"));
    }
}
