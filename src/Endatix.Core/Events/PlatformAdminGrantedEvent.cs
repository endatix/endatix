using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Event dispatched when platform administrator access is granted.
/// </summary>
public sealed class PlatformAdminGrantedEvent(long userId) : DomainEventBase
{
    public long UserId { get; init; } = userId;
}
