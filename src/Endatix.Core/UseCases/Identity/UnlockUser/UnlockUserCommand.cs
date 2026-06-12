using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Identity.UnlockUser;

/// <summary>
/// Command to unlock a user
/// </summary>
/// <param name="UserId">The ID of the user to unlock</param>
/// <returns>A result indicating the success or failure of the operation</returns>
public sealed record UnlockUserCommand(long UserId) : ICommand<Result>;
