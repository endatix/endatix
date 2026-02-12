using System.IO.Pipelines;
using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Exporting.ColumnDefinitions;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Exporting.Exporters.Submissions;

/// <summary>
/// JSON exporter for submission data, optimized for streaming and low memory usage.
/// </summary>
public sealed class SubmissionJsonExporter(
    ILogger<SubmissionJsonExporter> logger,
    IEnumerable<IValueTransformer> globalTransformers) : SubmissionExporterBase(logger, globalTransformers)
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

            await foreach ((var row, var doc, var columns) in GetStreamContextAsync(records, options, cancellationToken))
            {
                using (doc)
                {
                    firstRow ??= row;
                    var context = new TransformationContext<SubmissionExportRow>(row, doc, _logger);
                    jsonWriter.WriteStartObject();
                    foreach (var col in columns)
                    {
                        jsonWriter.WritePropertyName(col.JsonPropertyName);
                        try
                        {
                            var value = col.GetValue(context);
                            WriteValue(jsonWriter, value);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error processing column {ColumnName} for row {RowId}", col.Name, row.Id);
                            jsonWriter.WriteStringValue(NOT_AVAILABLE_VALUE);
                        }
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
            case JsonElement element:
                element.WriteTo(writer);
                break;
            case JsonNode node:
                writer.WriteRawValue(node.ToJsonString());
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
