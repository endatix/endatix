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

    public override async Task<Result<FileExport>> StreamExportAsync(
        IAsyncEnumerable<SubmissionExportRow> records,
        ExportOptions? options,
        CancellationToken cancellationToken,
        PipeWriter writer)
    {
        try
        {
            using var stream = writer.AsStream();
            await using var jsonWriter = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true });

            jsonWriter.WriteStartArray();
            SubmissionExportRow? firstRow = null;

            await foreach (var (row, doc, columns) in GetStreamContextAsync(records, options, cancellationToken))
            {
                using (doc) // Ensure the document is disposed after each row
                {
                    firstRow ??= row;
                    jsonWriter.WriteStartObject();
                    foreach (var col in columns)
                    {
                        jsonWriter.WritePropertyName(col.JsonPropertyName);
                        WriteValue(jsonWriter, col.ExtractValue(row, doc));
                    }
                    jsonWriter.WriteEndObject();
                }
            }

            jsonWriter.WriteEndArray();
            await jsonWriter.FlushAsync(cancellationToken);
            await writer.FlushAsync(cancellationToken);

            return Result<FileExport>.Success(new FileExport(ContentType, GetFileName(options, firstRow, FileExtension)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting submissions to JSON");
            return Result<FileExport>.Error($"Failed to export submissions: {ex.Message}");
        }
    }

    private static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                break;
            case string s:
                writer.WriteStringValue(s);
                break;
            case bool b:
                writer.WriteBooleanValue(b);
                break;
            case DateTime dt:
                writer.WriteStringValue(dt);
                break;
            case int i:
                writer.WriteNumberValue(i);
                break;
            case long l:
                writer.WriteNumberValue(l);
                break;
            case double d:
                writer.WriteNumberValue(d);
                break;
            case decimal dec:
                writer.WriteNumberValue(dec);
                break;
            default:
                writer.WriteStringValue(value.ToString());
                break;
        }
    }
}
