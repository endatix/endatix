using System.Globalization;
using System.IO.Pipelines;
using System.Text.Json;
using Ardalis.GuardClauses;
using CsvHelper;
using CsvHelper.Configuration;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.Submissions;

/// <summary>
/// CSV exporter for submission data, optimized for streaming and low memory usage.
/// </summary>
public sealed class SubmissionCsvExporter(ILogger<SubmissionCsvExporter> logger) : SubmissionExporterBase(logger)
{
    public override string Format => "csv";
    public override string ContentType => "text/csv";

    /// <summary>
    /// Streams the export directly to the provided PipeWriter.
    /// </summary>
    public override async Task<Result<FileExport>> StreamExportAsync(
        IAsyncEnumerable<SubmissionExportRow> records,
        ExportOptions? options,
        CancellationToken cancellationToken,
        PipeWriter writer)
    {
        Guard.Against.Null(writer);

        try
        {
            using var stream = writer.AsStream();
            using var streamWriter = new StreamWriter(stream, leaveOpen: true);
            using var csv = new CsvWriter(streamWriter, new CsvConfiguration(CultureInfo.InvariantCulture));

            List<ColumnDefinition<SubmissionExportRow>>? columns = null;

            await foreach (var row in records.WithCancellation(cancellationToken))
            {
                if (columns is null)
                {
                    columns = BuildColumns(row, options).ToList();
                    foreach (var col in columns)
                    {
                        csv.WriteField(col.Name);
                    }
                    await csv.NextRecordAsync();
                }

                using var jsonDoc = string.IsNullOrWhiteSpace(row.AnswersModel)
                    ? null
                    : JsonDocument.Parse(row.AnswersModel);

                foreach (var column in columns)
                {
                    csv.WriteField(column.GetFormattedValue(row, jsonDoc));
                }
                await csv.NextRecordAsync();
            }

            await writer.FlushAsync(cancellationToken);

            var fileExport = new FileExport(
                fileName: GetFileName(options, null, FileExtension),
                contentType: ContentType);

            return Result<FileExport>.Success(fileExport);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting submissions to CSV");
            return Result<FileExport>.Error($"Failed to export submissions: {ex.Message}");
        }
    }
}