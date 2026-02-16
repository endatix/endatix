using System.IO.Pipelines;
using System.Text.Json;
using System.Text.Json.Nodes;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Exporting.Exporters.Submissions;

/// <summary>
/// JSON exporter for submission data, optimized for streaming and low memory usage.
/// </summary>
internal sealed partial class SubmissionJsonExporter(
    ILogger<SubmissionJsonExporter> logger,
    IEnumerable<IValueTransformer> globalTransformers) : SubmissionExporterBase(logger, globalTransformers)
{
    public override string Format => "json";
    public override string ContentType => "application/json";

    private long? _formId = null;

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
                    _formId ??= row.FormId;
                    var context = new TransformationContext<SubmissionExportRow>(row, doc, _logger);
                    jsonWriter.WriteStartObject();
                    foreach (var col in columns)
                    {
                        jsonWriter.WritePropertyName(col.JsonPropertyName);
                        try
                        {
                            var value = col.GetValue(context);
                            switch (value)
                            {
                                case null:
                                    jsonWriter.WriteNullValue();
                                    break;
                                case JsonElement el:
                                    el.WriteTo(jsonWriter);
                                    break;
                                case JsonNode node:
                                    node.WriteTo(jsonWriter);
                                    break;
                                default:
                                    JsonSerializer.Serialize(jsonWriter, value);
                                    break;
                            }
                        }
                        catch (Exception ex)
                        {
                            LogCellLevelError(col.Name, row.Id, ex);
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
            LogExportError(ex, _formId);
            return Result<FileExport>.Error($"Failed to export submissions: {ex.Message}");
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Error processing column {ColumnName} for row {RowId}")]
    private partial void LogCellLevelError(string columnName, long rowId, Exception ex);

    [LoggerMessage(Level = LogLevel.Error, Message = "Error exporting submissions to JSON for form {FormId:L}")]
    private partial void LogExportError(Exception ex, long? formId);
}
