using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Event dispatched when platform administrator access is revoked.
/// </summary>
public sealed class PlatformAdminRevokedEvent(long userId) : DomainEventBase
{
    public long UserId { get; init; } = userId;
}
