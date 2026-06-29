using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.UnlockUser;


/// <summary>
/// Handler for unlocking a user
/// </summary>
/// <param name="userService">The user service to use</param>
/// <returns>A result indicating the success or failure of the operation</returns>
public sealed class UnlockUserHandler(IUserService userService) : ICommandHandler<UnlockUserCommand, Result>
{
    /// <inheritdoc />
    public Task<Result> Handle(UnlockUserCommand request, CancellationToken cancellationToken)
    {
        return userService.UnlockUserAsync(request.UserId, cancellationToken);
    }
}
