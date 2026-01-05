using MediatR;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;
using System.Runtime.CompilerServices;

namespace Endatix.Core.UseCases.Submissions.Export;

public sealed class SubmissionsExportHandler : IRequestHandler<SubmissionsExportQuery, Result<FileExport>>
{
    private readonly ISubmissionExportRepository _exportRepository;
    private readonly ILogger<SubmissionsExportHandler> _logger;

    public SubmissionsExportHandler(
        ISubmissionExportRepository exportRepository,
        ILogger<SubmissionsExportHandler> logger)
    {
        _exportRepository = exportRepository;
        _logger = logger;
    }

    public async Task<Result<FileExport>> Handle(SubmissionsExportQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var itemType = request.Exporter.ItemType;

            if (!typeof(IExportItem).IsAssignableFrom(itemType))
            {
                _logger.LogWarning("Exporter {Exporter} has item type {ItemType} which does not implement IExportItem", request.Exporter, itemType.Name);
                return Result.Invalid(new ValidationError($"Invalid item type: {itemType.Name}"));
            }

            return await request.Exporter.StreamExportAsync(
                getDataAsync: type => GetExportRowsByType(type, request.FormId, request.SqlFunctionName, cancellationToken),
                options: request.Options,
                cancellationToken: cancellationToken,
                writer: request.OutputWriter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting submissions for form {FormId}", request.FormId);
            return Result<FileExport>.Error($"Export failed: {ex.Message}");
        }
    }

    private async IAsyncEnumerable<IExportItem> GetExportRowsByType(
        Type itemType,
        long formId,
        string? sqlFunctionName,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (itemType == typeof(SubmissionExportRow))
        {
            await foreach (var item in _exportRepository.GetExportRowsAsync<SubmissionExportRow>(formId, sqlFunctionName, cancellationToken))
            {
                yield return item;
            }
        }
        else if (itemType == typeof(DynamicExportRow))
        {
            await foreach (var item in _exportRepository.GetExportRowsAsync<DynamicExportRow>(formId, sqlFunctionName, cancellationToken))
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