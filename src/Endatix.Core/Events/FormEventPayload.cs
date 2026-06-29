using Endatix.Core.Entities;

namespace Endatix.Core.Events;

/// <summary>
/// Builds the shared <c>form.*</c> integration-event payload so the four Form events stay in lockstep
/// (one place to add a field). All form events carry the same shape, including <c>folderId</c>. IDs are
/// serialized as strings on the wire by the outbox dispatcher's LongToStringConverter.
/// </summary>
internal static class FormEventPayload
{
    /// <param name="isEnabled">
    /// Optional override letting an event supply its own captured enabled state, so the payload reflects the
    /// value at event-creation time rather than re-reading the live form. Defaults to the form's current value.
    /// </param>
    /// <param name="revision">
    /// Optional override letting an event supply the revision captured at event-raise time, so events queued
    /// together in one transaction keep their own monotonic revision instead of all reading the final value.
    /// Defaults to the form's current revision.
    /// </param>
    public static object Create(Form form, bool? isEnabled = null, long? revision = null) => new
    {
        formId = form.Id,
        tenantId = form.TenantId,
        name = form.Name,
        description = form.Description,
        isEnabled = isEnabled ?? form.IsEnabled,
        activeDefinitionId = form.ActiveDefinitionId,
        themeId = form.ThemeId,
        folderId = form.FolderId,
        createdAt = form.CreatedAt,
        modifiedAt = form.ModifiedAt,
        revision = revision ?? form.Revision,
    };
}
