using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Event dispatched when a form's enabled state changes. Also an <see cref="IIntegrationEvent"/> — captured to
/// the outbox and delivered as the <c>form.enabled_state_changed</c> webhook by the relay.
/// </summary>
public sealed class FormEnabledStateChangedEvent(Form form, bool isEnabled) : DomainEventBase, IIntegrationEvent
{
    public Form Form { get; init; } = form;
    public bool IsEnabled { get; init; } = isEnabled;

    // Revision captured at raise time, so the payload keeps this event's revision (not a later one).
    private readonly long _revision = form.Revision;

    /// <inheritdoc />
    public string EventType => "form.enabled_state_changed";

    /// <inheritdoc />
    public object GetPayload() => FormEventPayload.Create(Form, IsEnabled, _revision);
}
