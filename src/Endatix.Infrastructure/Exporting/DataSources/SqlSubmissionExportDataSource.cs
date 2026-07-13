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
        SqlSubmissionExportStreamHelper.SupportsItemType(request.ItemType);

    public Task<Result<ExportOptions>> PrepareOptionsAsync(
        ExportDataSourceContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(context.Options));

    public IAsyncEnumerable<IExportItem> StreamAsync(
        ExportDataSourceContext context,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(context.Request.SqlFunctionName))
        {
            throw new InvalidOperationException("SQL export data source requires SqlFunctionName.");
        }

        return SqlSubmissionExportStreamHelper.StreamAsync(
            exportRepository,
            context.FormId,
            context.Request.ItemType,
            context.Request.SqlFunctionName,
            context.ExportPageSize,
            cancellationToken);
    }
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
        SqlSubmissionExportStreamHelper.SupportsItemType(request.ItemType);

    public Task<Result<ExportOptions>> PrepareOptionsAsync(
        ExportDataSourceContext context,
        CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success(context.Options));

    public IAsyncEnumerable<IExportItem> StreamAsync(
        ExportDataSourceContext context,
        CancellationToken cancellationToken) =>
        SqlSubmissionExportStreamHelper.StreamAsync(
            exportRepository,
            context.FormId,
            context.Request.ItemType,
            sqlFunctionName: null,
            context.ExportPageSize,
            cancellationToken);
}

/// <summary>
/// Shared streaming logic for legacy SQL export data sources (removed in E13).
/// </summary>
internal static class SqlSubmissionExportStreamHelper
{
    internal static bool SupportsItemType(Type itemType) =>
        itemType == typeof(SubmissionExportRow) || itemType == typeof(DynamicExportRow);

    internal static async IAsyncEnumerable<IExportItem> StreamAsync(
        ISubmissionExportRepository exportRepository,
        long formId,
        Type itemType,
        string? sqlFunctionName,
        int? exportPageSize,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (itemType == typeof(SubmissionExportRow))
        {
            await foreach (SubmissionExportRow row in exportRepository.GetExportRowsAsync<SubmissionExportRow>(
                               formId,
                               sqlFunctionName,
                               exportPageSize,
                               cancellationToken))
            {
                yield return row;
            }

            yield break;
        }

        if (itemType == typeof(DynamicExportRow))
        {
            await foreach (DynamicExportRow row in exportRepository.GetExportRowsAsync<DynamicExportRow>(
                               formId,
                               sqlFunctionName,
                               exportPageSize,
                               cancellationToken))
            {
                yield return row;
            }

            yield break;
        }

        throw new InvalidOperationException($"Unsupported SQL export item type: {itemType.Name}");
    }
}
