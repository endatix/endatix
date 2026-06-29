using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Event dispatched when a new form is created. Also an <see cref="IIntegrationEvent"/> — captured to the
/// outbox and delivered as the <c>form.created</c> webhook by the relay.
/// </summary>
public sealed class FormCreatedEvent(Form form) : DomainEventBase, IIntegrationEvent
{
    public Form Form { get; init; } = form;

    /// <inheritdoc />
    public string EventType => "form.created";

    /// <inheritdoc />
    public object GetPayload() => FormEventPayload.Create(Form);
}
