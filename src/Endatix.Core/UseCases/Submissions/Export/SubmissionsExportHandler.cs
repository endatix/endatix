using MediatR;
using Endatix.Core.Abstractions.Repositories;
using Microsoft.Extensions.Logging;
using Ardalis.GuardClauses;
using Endatix.Core.Abstractions.Exporting;

namespace Endatix.Core.UseCases.Submissions.Export;

public sealed class SubmissionsExportHandler : IRequestHandler<SubmissionsExportQuery, ExportResult>
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

    public async Task<ExportResult> Handle(SubmissionsExportQuery request, CancellationToken cancellationToken)
    {
        try
        {
            Guard.Against.Null(request.Exporter);

            // Get the export data stream
            var exportRows = _exportRepository.GetExportRowsAsync(request.FormId, cancellationToken);

            // Stream the export directly to the provided output stream
            var exportResult = await request.Exporter.StreamExportAsync(
                records: exportRows,
                options: request.Options,
                cancellationToken: cancellationToken,
                outputStream: request.OutputStream);

            return ExportResult.Success(exportResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting submissions for form {FormId}", request.FormId);
            return ExportResult.Failure($"Export failed: {ex.Message}");
        }
    }
}