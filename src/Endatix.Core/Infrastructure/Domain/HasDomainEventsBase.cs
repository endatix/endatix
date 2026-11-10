using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;

namespace Endatix.Core.Infrastructure.Domain;

public abstract class HasDomainEventsBase
{
    private readonly List<DomainEventBase> _domainEvents = new();
    /// <summary>Domain events registered on this entity and not yet cleared.</summary>
    [NotMapped]
    public IEnumerable<DomainEventBase> DomainEvents => _domainEvents.AsReadOnly();

    /// <summary>Registers a domain event to be dispatched or captured when the entity is saved.</summary>
    protected void RegisterDomainEvent(DomainEventBase domainEvent) => _domainEvents.Add(domainEvent);

    /// <summary>
    /// Clears the registered domain events. Called after they have been captured/dispatched
    /// (e.g. by the outbox capture in <c>AppDbContext.ProcessEntities</c>).
    /// </summary>
    public void ClearDomainEvents() => _domainEvents.Clear();

    /// <summary>
    /// Removes the specified domain events (e.g. the integration events captured to the outbox),
    /// leaving any other registered events in place.
    /// </summary>
    public void RemoveDomainEvents(IEnumerable<DomainEventBase> domainEvents)
    {
        var toRemove = domainEvents.ToHashSet();
        _domainEvents.RemoveAll(toRemove.Contains);
    }
}
