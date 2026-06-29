using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Event dispatched when a form is updated. Also an <see cref="IIntegrationEvent"/> — captured to the outbox
/// and delivered as the <c>form.updated</c> webhook by the relay.
/// </summary>
public sealed class FormUpdatedEvent(Form form) : DomainEventBase, IIntegrationEvent
{
    public Form Form { get; init; } = form;

    /// <inheritdoc />
    public string EventType => "form.updated";

    /// <inheritdoc />
    public object GetPayload() => FormEventPayload.Create(Form);
}
