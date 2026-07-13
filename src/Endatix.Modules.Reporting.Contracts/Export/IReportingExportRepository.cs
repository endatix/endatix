namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Streams processed flattened submissions for schema-driven export.
/// </summary>
public interface IReportingExportRepository
{
    Task<bool> HasExportableRowsAsync(long tenantId, long formId, CancellationToken cancellationToken);

    IAsyncEnumerable<FlattenedExportRow> StreamFlattenedSubmissionsAsync(
        long tenantId,
        long formId,
        ExportQueryOptions options,
        CancellationToken cancellationToken);
}
