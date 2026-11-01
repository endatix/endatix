using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Features.SubmissionGrid;
using FormSchemaEntity = Endatix.Modules.Reporting.Domain.FormSchema;

namespace Endatix.Modules.Reporting.Features.SearchCopy;

/// <summary>
/// A form's search keys and how far its rows have caught up with them.
/// </summary>
/// <param name="Keys">The form's search keys.</param>
/// <param name="OldestRowRevision">
/// The oldest search keys revision a prepared row of the form is stamped with; null when no row is prepared.
/// </param>
internal sealed record FormSearchKeyStatus(FormSearchKeys Keys, int? OldestRowRevision)
{
    public SubmissionGridSearchSlots Slots =>
        new(Keys.Keys.Count, FormSearchKeys.MaxKeys, Keys.UserKeyCount, FormSearchKeys.MaxUserKeys);

    public static async Task<FormSearchKeyStatus> ReadAsync(
        FormSchemaEntity schema,
        IFlattenedSubmissionRepository rows,
        CancellationToken cancellationToken)
    {
        var oldest = await rows.OldestSearchKeysRevisionAsync(schema.TenantId, schema.FormId, cancellationToken);
        return new FormSearchKeyStatus(FormSearchKeys.Of(schema), oldest);
    }

    /// <summary>The schema's search keys as the grid shows them, with how far its rows have caught up.</summary>
    public static async Task<SubmissionGridSearchKeys> ReadGridSearchKeysAsync(
        FormSchemaEntity schema,
        IFlattenedSubmissionRepository rows,
        CancellationToken cancellationToken) =>
        (await ReadAsync(schema, rows, cancellationToken)).ToGridSearchKeys(schema.FormId);

    /// <summary>Whether some prepared row was written before <paramref name="key"/> and not rebuilt since.</summary>
    public bool IsPending(SearchKey key) => OldestRowRevision < key.Revision;

    public SubmissionGridSearchKeys ToGridSearchKeys(long formId) =>
        new(
            formId,
            Keys.Keys.Select(key => new SubmissionGridSearchKey(key.Key, key.Source, key.FilterType, IsPending(key))).ToList(),
            Slots);
}
