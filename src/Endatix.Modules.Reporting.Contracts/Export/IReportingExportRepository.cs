namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Streams processed flattened submissions for schema-driven export.
/// </summary>
public interface IReportingExportRepository
{
    /// <summary>
    /// Checks if the repository has exportable rows for the given tenant and form.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant.</param>
    /// <param name="formId">The ID of the form.</param>
    /// <param name="options">The export query options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if the repository has exportable rows, false otherwise.</returns>
    Task<bool> HasExportableRowsAsync(
        long tenantId,
        long formId,
        ExportQueryOptions options,
        CancellationToken cancellationToken);

    /// <summary>
    /// Streams flattened submissions for the given tenant and form.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant.</param>
    /// <param name="formId">The ID of the form.</param>
    /// <param name="options">The export query options.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>An async enumerable of flattened export rows.</returns>
    IAsyncEnumerable<FlattenedExportRow> StreamFlattenedSubmissionsAsync(
        long tenantId,
        long formId,
        ExportQueryOptions options,
        CancellationToken cancellationToken);
}
