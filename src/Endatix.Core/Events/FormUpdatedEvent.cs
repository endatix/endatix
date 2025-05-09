using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Event dispatched when a form is updated.
/// </summary>
public sealed class FormUpdatedEvent(Form form) : DomainEventBase
{
    public Form Form { get; init; } = form;
}
