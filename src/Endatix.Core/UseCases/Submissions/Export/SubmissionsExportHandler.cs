using MediatR;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;
using System.Reflection;

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
            // Use reflection to call the generic ExecuteExportAsync method based on the exporter's item type
            var itemType = request.Exporter.ItemType;
            var method = typeof(SubmissionsExportHandler)
                .GetMethod(nameof(ExecuteExportAsync), BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new InvalidOperationException("Could not find ExecuteExportAsync method");

            var genericMethod = method.MakeGenericMethod(itemType);
            var task = (Task<Result<FileExport>>)genericMethod.Invoke(this, new object?[] { request, cancellationToken })!;

            return await task;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting submissions for form {FormId}", request.FormId);
            return Result<FileExport>.Error($"Export failed: {ex.Message}");
        }
    }

    private async Task<Result<FileExport>> ExecuteExportAsync<T>(SubmissionsExportQuery request, CancellationToken cancellationToken) where T : class
    {
        var exportRows = _exportRepository.GetExportRowsAsync<T>(request.FormId, request.SqlFunctionName, cancellationToken);
        var exporter = (IExporter<T>)request.Exporter;

        return await exporter.StreamExportAsync(
            records: exportRows,
            options: request.Options,
            cancellationToken: cancellationToken,
            writer: request.OutputWriter);
    }
}