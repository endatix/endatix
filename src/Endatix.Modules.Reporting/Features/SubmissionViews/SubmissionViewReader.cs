using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;

namespace Endatix.Modules.Reporting.Features.SubmissionViews;

/// <summary>
/// Turns a stored view into the view the grid reads, the same way for a read and for a save's response.
/// </summary>
internal sealed class SubmissionViewReader(IFormSchemaRepository formSchemaRepository)
{
    /// <summary>
    /// The view's version and its settings without the columns the form no longer has. Before the form's schema is
    /// compiled there is nothing to judge the columns by, so they come back as stored.
    /// </summary>
    public async Task<SubmissionViewDto> ReadAsync(SubmissionView view, CancellationToken cancellationToken)
    {
        var schema = await formSchemaRepository.GetByFormIdAsync(view.TenantId, view.FormId, cancellationToken);
        if (schema is null)
        {
            return new SubmissionViewDto(view.Version, view.SettingsJson);
        }

        var schemaKeys = FormSchemaFlatteningMap.FromJson(schema.FlatteningMap).Columns
            .Select(column => column.Key)
            .ToHashSet(StringComparer.Ordinal);
        return new SubmissionViewDto(view.Version, SubmissionViewColumns.KeepKnown(view.SettingsJson, schemaKeys));
    }
}
