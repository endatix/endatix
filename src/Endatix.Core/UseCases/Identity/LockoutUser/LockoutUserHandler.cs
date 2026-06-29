using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.LockoutUser;

/// <summary>
/// Handler for locking out a user
/// </summary>
/// <param name="userService">The user service to use</param>
/// <returns>A result indicating the success or failure of the operation</returns>
public sealed class LockoutUserHandler(IUserService userService) : ICommandHandler<LockoutUserCommand, Result>
{
    /// <inheritdoc />
    public Task<Result> Handle(LockoutUserCommand request, CancellationToken cancellationToken)
    {
        return userService.LockoutUserAsync(request.UserId, cancellationToken);
    }
}
