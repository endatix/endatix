using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Event dispatched when a new form is created.
/// </summary>
public sealed class FormCreatedEvent(Form form) : DomainEventBase
{
    public Form Form { get; init; } = form;
}
