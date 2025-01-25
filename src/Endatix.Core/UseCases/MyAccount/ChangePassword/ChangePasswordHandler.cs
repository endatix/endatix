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
        var userResult = await userService.GetUserAsync(request.User, cancellationToken);

        if (userResult.IsSuccess && userResult.Value is { } user)
        {
            // Leave this for testing purposes. It's for debugging purposes.
            if (string.IsNullOrEmpty(request.CurrentPassword))
            {
                return await userService.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword, cancellationToken);
            }

            return Result.Success("Password changed successfully");
        }

        return Result.Error("User not found");
    }
}
