using Endatix.Core.Entities.Identity;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// A domain event that is dispatched whenever a user logs on.
/// The <c>LoginHandler</c> is used to dispatch this event.
/// </summary>
public sealed class UserLoggedInEvent(User user) : DomainEventBase
{
    public User User { get; init; } = user;
}