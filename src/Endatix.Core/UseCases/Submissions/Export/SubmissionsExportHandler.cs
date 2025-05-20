using MediatR;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;

namespace Endatix.Core.UseCases.Submissions.Export;

public sealed class SubmissionsExportHandler : IRequestHandler<SubmissionsExportQuery, Result<FileExport>>
{
    private readonly ISubmissionExportRepository _exportRepository;
    private readonly IFormsRepository _formsRepository;
    private readonly ILogger<SubmissionsExportHandler> _logger;

    public SubmissionsExportHandler(
        ISubmissionExportRepository exportRepository,
        IFormsRepository formsRepository,
        ILogger<SubmissionsExportHandler> logger)
    {
        _exportRepository = exportRepository;
        _formsRepository = formsRepository;
        _logger = logger;
    }

    public async Task<Result<FileExport>> Handle(SubmissionsExportQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Get options with FormId in metadata
            var options = request.GetOptionsWithFormId();
            var form = await _formsRepository.GetByIdAsync(request.FormId, cancellationToken);
            if (form is null)
            {
                return Result<FileExport>.NotFound($"Form with ID {request.FormId} not found");
            }

            // Get the export data stream
            var exportRows = _exportRepository.GetExportRowsAsync(request.FormId, cancellationToken);
            
            // Stream the export directly to the provided output stream
            return await request.Exporter.StreamExportAsync(
                records: exportRows,
                options: options,
                cancellationToken: cancellationToken,
                outputStream: request.OutputStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting submissions for form {FormId}", request.FormId);
            return Result<FileExport>.Error($"Export failed: {ex.Message}");
        }
    }
}