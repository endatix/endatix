using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using MediatR;
using System.IO.Pipelines;

namespace Endatix.Core.UseCases.Submissions.Export;

public sealed record SubmissionsExportQuery(
    long FormId,
    IExporter Exporter,
    ExportOptions Options,
    PipeWriter OutputWriter,
    string? SqlFunctionName = null
) : IRequest<Result<FileExport>>; 