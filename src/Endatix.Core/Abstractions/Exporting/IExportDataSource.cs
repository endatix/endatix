using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions.Exporting;

/// <summary>
/// Identifies an export request for data-source resolution.
/// </summary>
/// <param name="Format">
/// The export format to use.
/// </param>
/// <param name="ItemType">
/// The type of item to export.
/// </param>
/// <param name="SqlFunctionName">
/// When set, selects a custom SQL function for export.
/// </param>
/// <param name="ExportFormatId">
/// When set, selects reporting read-model data sources. Legacy SQL default / CustomExports
/// leave this null so <c>TabularExportDataSource</c> does not steal built-in CSV.
/// </param>
public sealed record ExportDataSourceRequest(
    string Format,
    Type ItemType,
    string? SqlFunctionName,
    long? ExportFormatId = null);

/// <summary>
/// Runtime context passed to an export data source.
/// </summary>
public sealed record ExportDataSourceContext(
    ExportDataSourceRequest Request,
    long TenantId,
    long FormId,
    ExportOptions Options,
    int? ExportPageSize);

/// <summary>
/// Supplies export rows for a specific format/item-type combination.
/// Custom integrations (e.g. Shoji codebook) implement this in feature slices.
/// </summary>
public interface IExportDataSource
{
    bool Matches(ExportDataSourceRequest request);

    Task<Result<ExportOptions>> PrepareOptionsAsync(
        ExportDataSourceContext context,
        CancellationToken cancellationToken);

    IAsyncEnumerable<IExportItem> StreamAsync(
        ExportDataSourceContext context,
        CancellationToken cancellationToken);
}

/// <summary>
/// Resolves the data source for an export request.
/// </summary>
public interface IExportDataSourceResolver
{
    IExportDataSource Resolve(ExportDataSourceRequest request);
}
