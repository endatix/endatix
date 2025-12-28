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

            SubmissionExportRow? firstRow = null;
            var headerWritten = false;

            await foreach (var (row, doc, columns) in GetStreamContextAsync(records, options, cancellationToken))
            {
                using (doc)
                {
                    firstRow ??= row;

                    if (!headerWritten)
                    {
                        foreach (var col in columns)
                        {
                            csv.WriteField(col.Name);
                        }
                        await csv.NextRecordAsync();
                        headerWritten = true;
                    }

                    foreach (var col in columns)
                    {
                        csv.WriteField(col.GetFormattedValue(row, doc));
                    }
                    await csv.NextRecordAsync();
                }
            }

            await streamWriter.FlushAsync();
            await writer.FlushAsync(cancellationToken);

            return Result<FileExport>.Success(new FileExport(ContentType, GetFileName(options, firstRow, FileExtension)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting submissions to CSV");
            return Result<FileExport>.Error($"Failed to export submissions: {ex.Message}");
        }
    }
}
