using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Tests.Shared;

/// <summary>
/// Flattened rows in the state the repository's conditional writes leave them in. Those writes run in the database,
/// so a test without one sets the row's columns directly.
/// </summary>
internal static class FlattenedRows
{
    /// <summary>A row processed with <paramref name="dataJson"/> from the version stamped <paramref name="sourceModifiedAt"/>.</summary>
    public static FlattenedSubmission Processed(
        FlattenedSubmissionKey key,
        string dataJson,
        DateTime sourceModifiedAt)
    {
        FlattenedSubmission row = new(key.SubmissionId, key.TenantId, key.FormId);
        row.Integration.MarkProcessed();
        Set(row, nameof(FlattenedSubmission.DataJson), dataJson);
        Set(row, nameof(FlattenedSubmission.SourceModifiedAt), sourceModifiedAt);
        Set(row, nameof(FlattenedSubmission.ModifiedAt), DateTime.UtcNow);
        return row;
    }

    private static void Set(FlattenedSubmission row, string property, object? value) =>
        typeof(FlattenedSubmission).GetProperty(property)!.SetValue(row, value);
}
