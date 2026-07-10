using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;

namespace Endatix.Core.Events;

/// <summary>
/// Raised when a form definition's schema JSON changes or a new active definition is published.
/// Captured to the outbox for durable reporting schema compilation.
/// </summary>
public sealed class FormDefinitionUpdatedEvent(Form form, FormDefinition formDefinition)
    : DomainEventBase, IIntegrationEvent
{
    public const string EventTypeName = "form.definition.updated";

    public Form Form { get; } = form;

    public FormDefinition FormDefinition { get; } = formDefinition;

    // Revision captured at raise time so the payload keeps this event's revision (not a later one).
    private readonly long _revision = form.Revision;

    public string EventType => EventTypeName;

    public object GetPayload() => new
    {
        formId = Form.Id,
        tenantId = Form.TenantId,
        formDefinitionId = FormDefinition.Id,
        revision = _revision,
    };
}
