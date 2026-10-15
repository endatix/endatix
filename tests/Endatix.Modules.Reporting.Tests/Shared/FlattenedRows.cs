using System.Reflection;
using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Domain;

namespace Endatix.Modules.Reporting.Tests.Shared;

/// <summary>
/// Flattened rows in the state the repository's conditional writes leave them in. Those writes run in the database,
/// so a test without one sets the row's columns directly.
/// </summary>
internal static class FlattenedRows
{
    /// <summary>
    /// A row processed with <paramref name="dataJson"/> from the version stamped <paramref name="sourceModifiedAt"/>,
    /// one second after that version, so its timestamps don't depend on the wall clock.
    /// </summary>
    public static FlattenedSubmission Processed(
        FlattenedSubmissionKey key,
        string dataJson,
        DateTime sourceModifiedAt)
    {
        var processedAt = DateTime.SpecifyKind(sourceModifiedAt, DateTimeKind.Utc).AddSeconds(1);
        FlattenedSubmission row = new(key, processedAt);
        Set(row.Integration, nameof(SubmissionIntegrationState.Code), SubmissionIntegrationStatusCodes.Processed);
        Set(row.Integration, nameof(SubmissionIntegrationState.LastAttemptAt), processedAt);
        Set(row.Integration, nameof(SubmissionIntegrationState.ProcessedAt), processedAt);
        Set(row, nameof(FlattenedSubmission.DataJson), dataJson);
        Set(row, nameof(FlattenedSubmission.SourceModifiedAt), sourceModifiedAt);
        Set(row, nameof(FlattenedSubmission.ModifiedAt), processedAt);
        return row;
    }

    // A get-only auto-property has no setter to call, so its compiler-generated backing field is written instead, the
    // way EF Core materializes it.
    private static void Set(object target, string property, object? value)
    {
        var type = target.GetType();
        var info = type.GetProperty(property)!;
        if (info.CanWrite)
        {
            info.SetValue(target, value);
            return;
        }

        type.GetField($"<{property}>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(target, value);
    }
}
