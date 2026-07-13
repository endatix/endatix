using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.Abstractions.Exporting;

/// <summary>
/// Identifies an export request for data-source resolution.
/// </summary>
public sealed record ExportDataSourceRequest(
    string Format,
    Type ItemType,
    string? SqlFunctionName);

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
