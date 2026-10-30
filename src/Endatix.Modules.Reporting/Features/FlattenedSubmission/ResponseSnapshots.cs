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
        (var filled, value) = column.Kind switch
        {
            FormSchemaColumnKind.Personalization => (true, PersonalizationColumns.ValueOf(column, Personalization)),
            FormSchemaColumnKind.SubmitterProfile => (true, SubmitterProfileColumns.ValueOf(column, SubmitterProfile)),
            _ => (false, (JsonElement?)null),
        };
        return filled;
    }
}
