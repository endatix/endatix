using System.Globalization;
using System.IO.Pipelines;
using Ardalis.GuardClauses;
using CsvHelper;
using CsvHelper.Configuration;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Exporting.Formatters;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Exporting.Exporters.Submissions;

/// <summary>
/// CSV exporter for submission data, optimized for streaming and low memory usage.
/// </summary>
public sealed class SubmissionCsvExporter(
    ILogger<SubmissionCsvExporter> logger,
    IEnumerable<IValueTransformer> globalTransformers) : SubmissionExporterBase(logger, globalTransformers)
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

            await foreach ((var row, var doc, var columns) in GetStreamContextAsync(records, options, cancellationToken))
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

                    var context = new TransformationContext<SubmissionExportRow>(row, doc, _logger);
                    foreach (var col in columns)
                    {
                        try
                        {
                            var value = col.GetValue(context);
                            var formattedValue = new DefaultCsvFormatter().Format(value, context);

                            csv.WriteField(formattedValue);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing column {ColumnName} for row {RowId}", col.Name, row.Id);
                            csv.WriteField(NOT_AVAILABLE_VALUE);
                        }
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
