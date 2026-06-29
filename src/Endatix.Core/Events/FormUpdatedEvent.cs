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
    public object GetPayload() => new
    {
        formId = Form.Id,
        tenantId = Form.TenantId,
        name = Form.Name,
        description = Form.Description,
        isEnabled = Form.IsEnabled,
        activeDefinitionId = Form.ActiveDefinitionId,
        themeId = Form.ThemeId,
        folderId = Form.FolderId,
        createdAt = Form.CreatedAt,
        modifiedAt = Form.ModifiedAt,
        revision = Form.Revision,
    };
}
