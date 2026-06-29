using Endatix.Core.Entities;

namespace Endatix.Core.Events;

/// <summary>
/// Builds the shared <c>form.*</c> integration-event payload so the four Form events stay in lockstep
/// (one place to add a field). All form events carry the same shape, including <c>folderId</c>. IDs are
/// serialized as strings on the wire by the outbox dispatcher's LongToStringConverter.
/// </summary>
internal static class FormEventPayload
{
    public static object Create(Form form) => new
    {
        formId = form.Id,
        tenantId = form.TenantId,
        name = form.Name,
        description = form.Description,
        isEnabled = form.IsEnabled,
        activeDefinitionId = form.ActiveDefinitionId,
        themeId = form.ThemeId,
        folderId = form.FolderId,
        createdAt = form.CreatedAt,
        modifiedAt = form.ModifiedAt,
        revision = form.Revision,
    };
}
