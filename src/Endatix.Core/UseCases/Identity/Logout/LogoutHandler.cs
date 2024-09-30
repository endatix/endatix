using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.Login;

/// <summary>
/// This class is responsible for handling the logout command.
/// It revokes the tokens for the user and returns the result of the operation.
/// </summary>
public class LogoutHandler(ITokenService tokenService, IUserService userService) : ICommandHandler<LogoutCommand, Result>
{
    public const string INVALID_LOGOUT_REQUEST = "Cannot log you out at this time. Please provide a valid response.";

    /// <summary>
    /// Handles the logout command by revoking the tokens for the user.
    /// </summary>
    /// <param name="request">The logout command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var userResult = await userService.GetUserAsync(request.ClaimsPrincipal, cancellationToken);
        if (userResult.IsSuccess && userResult is { } userToLogout)
        {
            var tokenRevocationResult = await tokenService.RevokeTokensAsync(userToLogout, cancellationToken);

            if (tokenRevocationResult.IsSuccess)
            {
                return Result.Success();
            }
        }

        return Result.Invalid(new ValidationError(INVALID_LOGOUT_REQUEST));
    }
}
