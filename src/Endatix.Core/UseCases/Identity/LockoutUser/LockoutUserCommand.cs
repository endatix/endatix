using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.LockoutUser;

/// <summary>
/// Command to lock out a user
/// </summary>
/// <param name="UserId">The ID of the user to lock out</param>
/// <returns>A result indicating the success or failure of the operation</returns>
public sealed record LockoutUserCommand(long UserId) : ICommand<Result>;
