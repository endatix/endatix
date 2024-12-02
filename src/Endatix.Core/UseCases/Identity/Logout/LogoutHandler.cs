using Endatix.Core.Abstractions;
using Endatix.Core.Events;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using MediatR;

namespace Endatix.Core.UseCases.Identity.Login;

/// <summary>
/// This class is responsible for handling the logout command.
/// It revokes the tokens for the user and returns the result of the operation.
/// </summary>
public class LogoutHandler(
    IUserTokenService tokenService,
    IUserService userService,
    IMediator mediator
    ) : ICommandHandler<LogoutCommand, Result<string>>
{
    public const string INVALID_LOGOUT_REQUEST_MESSAGE = "Invalid request or authentication state.";
    public const string SUCCESS_LOGOUT_MESSAGE = "User logged out successfully.";

    /// <summary>
    /// Handles the logout command by revoking the tokens for the user.
    /// </summary>
    /// <param name="request">The logout command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the operation.</returns>
    public async Task<Result<string>> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        var userResult = await userService.GetUserAsync(request.ClaimsPrincipal, cancellationToken);
        if (userResult.IsSuccess && userResult.Value is { } userToLogout)
        {
            var tokenRevocationResult = await tokenService.RevokeTokensAsync(userToLogout, cancellationToken);

            if (tokenRevocationResult.IsSuccess)
            {
                await mediator.Publish(new UserLoggedOutEvent(userToLogout), cancellationToken);

                return Result.Success(SUCCESS_LOGOUT_MESSAGE);
            }
        }

        return Result.Invalid(new ValidationError(INVALID_LOGOUT_REQUEST_MESSAGE));
    }
}
