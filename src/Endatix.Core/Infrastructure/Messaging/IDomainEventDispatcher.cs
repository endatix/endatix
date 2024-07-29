using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Infrastructure.Messaging;


/// <summary>
/// A simple interface for sending domain events. Can use MediatR or any other implementation.
/// </summary>
public interface IDomainEventDispatcher
{
  Task DispatchAndClearEvents(IEnumerable<EntityBase> entitiesWithEvents);
  Task DispatchAndClearEvents<TId>(IEnumerable<EntityBase<TId>> entitiesWithEvents) where TId : struct, IEquatable<TId>;
}