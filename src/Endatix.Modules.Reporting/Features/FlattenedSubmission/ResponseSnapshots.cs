using System.Text.Json;
using Endatix.Core.UseCases.Submissions;
using Endatix.Modules.Reporting.Features.FormSchema.FormSchema;
using Endatix.Modules.Reporting.Features.FormSchema.Personalization;
using Endatix.Modules.Reporting.Features.FormSchema.SubmitterProfile;

namespace Endatix.Modules.Reporting.Features.FlattenedSubmission;

/// <summary>
/// What a response carries besides its answers, as frozen on it: its audience values and the listed fields of its
/// submitter profile.
/// </summary>
internal sealed record ResponseSnapshots(
    PersonalizationRead? Personalization = null,
    IReadOnlyDictionary<string, string>? SubmitterProfile = null)
{
    public static readonly ResponseSnapshots None = new();

    /// <summary>
    /// The value of a column a snapshot fills, null when the snapshot lacks it; false for a column the answers fill.
    /// </summary>
    public bool TryGetValue(FormSchemaColumn column, out JsonElement? value)
    {
        value = column.Kind switch
        {
            FormSchemaColumnKind.Personalization => PersonalizationColumns.ValueOf(column, Personalization),
            FormSchemaColumnKind.SubmitterProfile => SubmitterProfileColumns.ValueOf(column, SubmitterProfile),
            _ => null,
        };
        return column.Kind is FormSchemaColumnKind.Personalization or FormSchemaColumnKind.SubmitterProfile;
    }
}
