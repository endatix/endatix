using MediatR;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Endatix.Core.UseCases.Submissions.Export;

public sealed class SubmissionsExportHandler(
    IExportDataSourceResolver dataSourceResolver,
    ILogger<SubmissionsExportHandler> logger) : IRequestHandler<SubmissionsExportQuery, Result<FileExport>>
{
    public async Task<Result<FileExport>> Handle(SubmissionsExportQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var itemType = request.Exporter.ItemType;

            if (!typeof(IExportItem).IsAssignableFrom(itemType))
            {
                logger.LogWarning(
                    "Exporter {Exporter} has item type {ItemType} which does not implement IExportItem",
                    request.Exporter,
                    itemType.Name);
                return Result.Invalid(new ValidationError($"Invalid item type: {itemType.Name}"));
            }

            ExportDataSourceRequest dataSourceRequest = new(
                request.Exporter.Format,
                itemType,
                request.SqlFunctionName);

            var dataSource = dataSourceResolver.Resolve(dataSourceRequest);
            ExportDataSourceContext dataSourceContext = new(
                dataSourceRequest,
                request.TenantId,
                request.FormId,
                request.Options,
                request.ExportPageSize);

            var prepareResult = await dataSource.PrepareOptionsAsync(
                dataSourceContext,
                cancellationToken);
            if (!prepareResult.IsSuccess)
            {
                return Result<FileExport>.Error(string.Join(", ", prepareResult.Errors));
            }

            var options = prepareResult.Value;

            return await request.Exporter.StreamExportAsync(
                getDataAsync: _ => StreamExportItemsAsync(dataSource, dataSourceContext, cancellationToken),
                options: options,
                cancellationToken: cancellationToken,
                writer: request.OutputWriter);
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                logger.LogWarning(
                    ex,
                    "Export cancelled for form {FormId}. SqlFunctionName: {SqlFunctionName}, ItemType: {ItemType}",
                    request.FormId,
                    request.SqlFunctionName ?? "(default)",
                    request.Exporter.ItemType.Name);
            }
            else
            {
                logger.LogError(
                    ex,
                    "Error exporting submissions for form {FormId}. SqlFunctionName: {SqlFunctionName}, ItemType: {ItemType}, InnerException: {InnerMessage}",
                    request.FormId,
                    request.SqlFunctionName ?? "(default)",
                    request.Exporter.ItemType.Name,
                    ex.InnerException?.Message ?? "(none)");
            }

            return Result<FileExport>.Error($"Export failed: {ex.Message}");
        }
    }

    private static async IAsyncEnumerable<IExportItem> StreamExportItemsAsync(
        IExportDataSource dataSource,
        ExportDataSourceContext context,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var item in dataSource.StreamAsync(context, cancellationToken))
        {
            yield return item;
        }
    }
}
