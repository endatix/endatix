using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.PlatformAdmin.GrantPlatformAdmin;

/// <summary>
/// Command to grant platform administrator access to a user.
/// </summary>
public sealed record GrantPlatformAdminCommand : ICommand<Result<string>>
{
    public GrantPlatformAdminCommand(long userId)
    {
        Guard.Against.NegativeOrZero(userId);

        UserId = userId;
    }

    public long UserId { get; }
}
