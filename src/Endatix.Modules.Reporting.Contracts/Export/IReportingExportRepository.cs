namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Streams processed flattened submissions for schema-driven export.
/// </summary>
public interface IReportingExportRepository
{
    /// <summary>
    /// Returns true when at least one processed flattened row matches the export query.
    /// </summary>
    Task<bool> HasExportableRowsAsync(
        long tenantId,
        long formId,
        ExportQueryOptions options,
        CancellationToken cancellationToken);

    /// <summary>
    /// Returns true when the form has at least one completed core submission (including tests).
    /// Used to distinguish "nothing to export" from "completed rows need backfill".
    /// </summary>
    Task<bool> HasCompletedSubmissionsAsync(
        long tenantId,
        long formId,
        CancellationToken cancellationToken);

    /// <summary>
    /// Streams flattened submissions for the given tenant and form.
    /// </summary>
    IAsyncEnumerable<FlattenedExportRow> StreamFlattenedSubmissionsAsync(
        long tenantId,
        long formId,
        ExportQueryOptions options,
        CancellationToken cancellationToken);
}
