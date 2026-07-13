using System.Runtime.CompilerServices;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Infrastructure.Exporting.DataSources;

/// <summary>
/// Streams submission exports from legacy SQL functions.
/// </summary>
internal sealed class SqlSubmissionExportDataSource(ISubmissionExportRepository exportRepository)
    : IExportDataSource, IPrioritizedExportDataSource
{
    public int Priority => ExportDataSourcePriority.SqlCustom;

    public bool Matches(ExportDataSourceRequest request) =>
        !string.IsNullOrWhiteSpace(request.SqlFunctionName) &&
        SupportsItemType(request.ItemType);

    public Task<Result<ExportOptions>> PrepareOptionsAsync(
        ExportDataSourceContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(context.Options));

    public async IAsyncEnumerable<IExportItem> StreamAsync(
        ExportDataSourceContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.Request.SqlFunctionName))
        {
            throw new InvalidOperationException("SQL export data source requires SqlFunctionName.");
        }

        if (context.Request.ItemType == typeof(SubmissionExportRow))
        {
            await foreach (var row in exportRepository.GetExportRowsAsync<SubmissionExportRow>(
                               context.FormId,
                               context.Request.SqlFunctionName,
                               context.ExportPageSize,
                               cancellationToken))
            {
                yield return row;
            }

            yield break;
        }

        if (context.Request.ItemType == typeof(DynamicExportRow))
        {
            await foreach (var row in exportRepository.GetExportRowsAsync<DynamicExportRow>(
                               context.FormId,
                               context.Request.SqlFunctionName,
                               context.ExportPageSize,
                               cancellationToken))
            {
                yield return row;
            }

            yield break;
        }

        throw new InvalidOperationException($"Unsupported SQL export item type: {context.Request.ItemType.Name}");
    }

    private static bool SupportsItemType(Type itemType) =>
        itemType == typeof(SubmissionExportRow) || itemType == typeof(DynamicExportRow);
}

/// <summary>
/// Fallback SQL export when no read-model integration handles the request.
/// </summary>
internal sealed class SqlDefaultSubmissionExportDataSource(ISubmissionExportRepository exportRepository)
    : IExportDataSource, IPrioritizedExportDataSource
{
    public int Priority => ExportDataSourcePriority.SqlFallback;

    public bool Matches(ExportDataSourceRequest request) =>
        string.IsNullOrWhiteSpace(request.SqlFunctionName) &&
        SupportsItemType(request.ItemType);

    public Task<Result<ExportOptions>> PrepareOptionsAsync(
        ExportDataSourceContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(context.Options));

    public async IAsyncEnumerable<IExportItem> StreamAsync(
        ExportDataSourceContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (context.Request.ItemType == typeof(SubmissionExportRow))
        {
            await foreach (var row in exportRepository.GetExportRowsAsync<SubmissionExportRow>(
                               context.FormId,
                               sqlFunctionName: null,
                               context.ExportPageSize,
                               cancellationToken))
            {
                yield return row;
            }

            yield break;
        }

        if (context.Request.ItemType == typeof(DynamicExportRow))
        {
            await foreach (var row in exportRepository.GetExportRowsAsync<DynamicExportRow>(
                               context.FormId,
                               sqlFunctionName: null,
                               context.ExportPageSize,
                               cancellationToken))
            {
                yield return row;
            }

            yield break;
        }

        throw new InvalidOperationException($"Unsupported SQL default export item type: {context.Request.ItemType.Name}");
    }

    private static bool SupportsItemType(Type itemType) =>
        itemType == typeof(SubmissionExportRow) || itemType == typeof(DynamicExportRow);
}
