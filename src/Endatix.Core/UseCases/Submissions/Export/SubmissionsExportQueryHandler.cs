using MediatR;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.UseCases.Submissions.Export;

public sealed class SubmissionsExportQueryHandler : IRequestHandler<SubmissionsExportQuery, ExportResult>
{
    private readonly ISubmissionExportRepository _exportRepository;
    private readonly ILogger<SubmissionsExportQueryHandler> _logger;

    public SubmissionsExportQueryHandler(
        ISubmissionExportRepository exportRepository,
        ILogger<SubmissionsExportQueryHandler> logger)
    {
        _exportRepository = exportRepository;
        _logger = logger;
    }

    public async Task<ExportResult> Handle(SubmissionsExportQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Get options with FormId in metadata
            var options = request.GetOptionsWithFormId();
            
            // Get the export data stream
            var exportRows = _exportRepository.GetExportRowsAsync(request.FormId, cancellationToken);
            
            // Stream the export directly to the provided output stream
            var exportResult = await request.Exporter.StreamExportAsync(
                records: exportRows,
                options: options,
                cancellationToken: cancellationToken,
                outputStream: request.OutputStream);
            
            // Return a result with headers information
            return ExportResult.Success(exportResult);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting submissions for form {FormId}", request.FormId);
            return ExportResult.Failure($"Export failed: {ex.Message}");
        }
    }
} 