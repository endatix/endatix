using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// A domain event that is dispatched whenever a new user is registered.
/// The <c>RegisterHandler</c> is used to dispatch this event.
/// </summary>
public sealed class UserRegisteredEvent(User user) : DomainEventBase
{
    public User User { get; init; } = user;
}