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

    /// <inheritdoc />
    public string EventType => "form.enabled_state_changed";

    /// <inheritdoc />
    public object GetPayload() => new
    {
        formId = Form.Id,
        tenantId = Form.TenantId,
        name = Form.Name,
        description = Form.Description,
        isEnabled = Form.IsEnabled,
        activeDefinitionId = Form.ActiveDefinitionId,
        themeId = Form.ThemeId,
        createdAt = Form.CreatedAt,
        modifiedAt = Form.ModifiedAt,
        revision = Form.Revision,
    };
}
