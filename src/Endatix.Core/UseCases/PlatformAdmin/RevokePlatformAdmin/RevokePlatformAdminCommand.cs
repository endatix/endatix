using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.PlatformAdmin.RevokePlatformAdmin;

/// <summary>
/// Command to revoke platform administrator access from a user.
/// </summary>
public sealed record RevokePlatformAdminCommand : ICommand<Result<string>>
{
    public RevokePlatformAdminCommand(long userId)
    {
        Guard.Against.NegativeOrZero(userId);

        UserId = userId;
    }

    public long UserId { get; }
}
