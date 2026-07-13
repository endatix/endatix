using MediatR;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Endatix.Core.UseCases.Submissions.Export;

public sealed class SubmissionsExportHandler : IRequestHandler<SubmissionsExportQuery, Result<FileExport>>
{
    private readonly ISubmissionExportRepository _exportRepository;
    private readonly ISubmissionExportReadModelProvider? _readModelProvider;
    private readonly ILogger<SubmissionsExportHandler> _logger;

    public SubmissionsExportHandler(
        ISubmissionExportRepository exportRepository,
        ILogger<SubmissionsExportHandler> logger,
        ISubmissionExportReadModelProvider? readModelProvider = null)
    {
        _exportRepository = exportRepository;
        _logger = logger;
        _readModelProvider = readModelProvider;
    }

    public async Task<Result<FileExport>> Handle(SubmissionsExportQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var itemType = request.Exporter.ItemType;

            if (!typeof(IExportItem).IsAssignableFrom(itemType))
            {
                _logger.LogWarning(
                    "Exporter {Exporter} has item type {ItemType} which does not implement IExportItem",
                    request.Exporter,
                    itemType.Name);
                return Result.Invalid(new ValidationError($"Invalid item type: {itemType.Name}"));
            }

            var options = request.Options;
            if (ShouldUseReportingReadModel(request, itemType))
            {
                var reportingOptionsResult = await PrepareReportingExportOptionsAsync(
                    request,
                    itemType,
                    cancellationToken);
                if (!reportingOptionsResult.IsSuccess)
                {
                    return Result<FileExport>.Error(string.Join(", ", reportingOptionsResult.Errors));
                }

                options = reportingOptionsResult.Value;
            }

            return await request.Exporter.StreamExportAsync(
                getDataAsync: type => GetExportRowsByType(
                    type,
                    request.TenantId,
                    request.FormId,
                    request.SqlFunctionName,
                    request.ExportPageSize,
                    cancellationToken),
                options: options,
                cancellationToken: cancellationToken,
                writer: request.OutputWriter);
        }
        catch (Exception ex)
        {
            if (ex is OperationCanceledException)
            {
                _logger.LogWarning(
                    ex,
                    "Export cancelled for form {FormId}. SqlFunctionName: {SqlFunctionName}, ItemType: {ItemType}",
                    request.FormId,
                    request.SqlFunctionName ?? "(default)",
                    request.Exporter.ItemType.Name);
            }
            else
            {
                _logger.LogError(
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

    private bool ShouldUseReportingReadModel(SubmissionsExportQuery request, Type itemType) =>
        _readModelProvider is not null &&
        string.IsNullOrWhiteSpace(request.SqlFunctionName) &&
        (itemType == typeof(SubmissionExportRow) || itemType == typeof(DynamicExportRow));

    private async Task<Result<ExportOptions>> PrepareReportingExportOptionsAsync(
        SubmissionsExportQuery request,
        Type itemType,
        CancellationToken cancellationToken)
    {
        if (itemType == typeof(DynamicExportRow))
        {
            return Result.Success(request.Options);
        }

        var prepareResult = await _readModelProvider!.PrepareSubmissionExportAsync(
            request.TenantId,
            request.FormId,
            cancellationToken);
        if (!prepareResult.IsSuccess)
        {
            return Result<ExportOptions>.Error(string.Join(", ", prepareResult.Errors));
        }

        request.Options.Metadata ??= new Dictionary<string, object>();
        request.Options.Metadata[SubmissionExportMetadataKeys.ColumnPlan] = prepareResult.Value;
        return Result.Success(request.Options);
    }

    private async IAsyncEnumerable<IExportItem> GetExportRowsByType(
        Type itemType,
        long tenantId,
        long formId,
        string? sqlFunctionName,
        int? exportPageSize,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (_readModelProvider is not null && string.IsNullOrWhiteSpace(sqlFunctionName))
        {
            if (itemType == typeof(SubmissionExportRow))
            {
                await foreach (var item in _readModelProvider.StreamSubmissionExportRowsAsync(
                                   tenantId,
                                   formId,
                                   exportPageSize,
                                   cancellationToken))
                {
                    yield return item;
                }

                yield break;
            }

            if (itemType == typeof(DynamicExportRow))
            {
                var codebookResult = await _readModelProvider.GenerateReportingCodebookJsonAsync(
                    tenantId,
                    formId,
                    cancellationToken);
                if (!codebookResult.IsSuccess)
                {
                    throw new InvalidOperationException(string.Join(", ", codebookResult.Errors));
                }

                yield return new DynamicExportRow { Data = codebookResult.Value };
                yield break;
            }
        }

        if (itemType == typeof(SubmissionExportRow))
        {
            await foreach (var item in _exportRepository.GetExportRowsAsync<SubmissionExportRow>(
                               formId,
                               sqlFunctionName,
                               exportPageSize,
                               cancellationToken))
            {
                yield return item;
            }
        }
        else if (itemType == typeof(DynamicExportRow))
        {
            await foreach (var item in _exportRepository.GetExportRowsAsync<DynamicExportRow>(
                               formId,
                               sqlFunctionName,
                               exportPageSize,
                               cancellationToken))
            {
                yield return item;
            }
        }
        else
        {
            throw new InvalidOperationException($"Unsupported export item type: {itemType.Name}");
        }
    }
}
