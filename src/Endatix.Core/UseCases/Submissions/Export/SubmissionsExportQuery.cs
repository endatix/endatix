using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Infrastructure.Result;
using MediatR;
using System.IO.Pipelines;

namespace Endatix.Core.UseCases.Submissions.Export;

/// <summary>
/// Streams a form's submissions through an exporter.
/// </summary>
/// <param name="FormId">The form whose submissions are exported.</param>
/// <param name="TenantId">The tenant the export runs for.</param>
/// <param name="Exporter">The exporter that writes the format.</param>
/// <param name="Options">Export options.</param>
/// <param name="OutputWriter">Where the exporter writes.</param>
/// <param name="SqlFunctionName">The SQL function to read rows from; null uses the default export function.</param>
/// <param name="ExportPageSize">Rows per page; null uses the default, 0 turns paging off.</param>
/// <param name="IncludeTestSubmissions">Keeps test submissions; only the default export function takes it.</param>
public sealed record SubmissionsExportQuery(
    long FormId,
    long TenantId,
    IExporter Exporter,
    ExportOptions Options,
    PipeWriter OutputWriter,
    string? SqlFunctionName = null,
    int? ExportPageSize = null,
    bool IncludeTestSubmissions = false
) : IRequest<Result<FileExport>>; 