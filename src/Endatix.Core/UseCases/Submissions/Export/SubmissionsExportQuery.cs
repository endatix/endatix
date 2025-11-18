using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using MediatR;
using System.IO.Pipelines;

namespace Endatix.Core.UseCases.Submissions.Export;

public sealed record SubmissionsExportQuery(
    long FormId,
    IExporter<SubmissionExportRow> Exporter,
    ExportOptions? Options,
    PipeWriter OutputWriter,
    long? ExportId = null
) : IRequest<Result<FileExport>>
{
    public ExportOptions GetOptionsWithFormId()
    {
        var options = Options ?? new ExportOptions();
        options.Metadata ??= new Dictionary<string, object>();
        
        // Add form ID to metadata if not already present
        if (!options.Metadata.ContainsKey("FormId"))
        {
            options.Metadata["FormId"] = FormId;
        }
        
        return options;
    }
} 