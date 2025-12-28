using System.IO.Pipelines;
using System.Text.Json;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.Submissions;

/// <summary>
/// JSON exporter for submission data, optimized for streaming and low memory usage.
/// </summary>
public sealed class SubmissionJsonExporter(ILogger<SubmissionJsonExporter> logger) : SubmissionExporterBase(logger)
{
    public override string Format => "json";
    public override string ContentType => "application/json";

    public override async Task<Result<FileExport>> StreamExportAsync(IAsyncEnumerable<SubmissionExportRow> records, ExportOptions? options, CancellationToken cancellationToken, PipeWriter writer)
    {
        try
        {
            using var stream = writer.AsStream();
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            var processedRecords = PrepareRecordsAsync(records, options, cancellationToken);

            await JsonSerializer.SerializeAsync(
                stream,
                processedRecords,
                jsonOptions,
                cancellationToken);

            await writer.FlushAsync(cancellationToken);

            var fileExport = new FileExport(
                fileName: GetFileName(options, null, FileExtension),
                contentType: ContentType);

            return Result<FileExport>.Success(fileExport);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting submissions to JSON");
            return Result<FileExport>.Error($"Failed to export submissions: {ex.Message}");
        }
    }
}
