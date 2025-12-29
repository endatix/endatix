using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.Submissions;

/// <summary>
/// Base class for submission exporters to reuse common logic for streaming and header generation.
/// </summary>
public abstract class SubmissionExporterBase(ILogger logger) : IExporter<SubmissionExportRow>
{
    protected const string NOT_AVAILABLE_VALUE = "N/A";

    protected readonly ILogger _logger = logger;

    private static readonly Dictionary<string, Func<SubmissionExportRow, object?>> _staticColumnAccessors = new()
    {
        [nameof(SubmissionExportRow.FormId)] = row => row.FormId,
        [nameof(SubmissionExportRow.Id)] = row => row.Id,
        [nameof(SubmissionExportRow.IsComplete)] = row => row.IsComplete,
        [nameof(SubmissionExportRow.CreatedAt)] = row => row.CreatedAt,
        [nameof(SubmissionExportRow.ModifiedAt)] = row => row.ModifiedAt,
        [nameof(SubmissionExportRow.CompletedAt)] = row => row.CompletedAt
    };

    /// <inheritdoc/>
    public abstract string Format { get; }

    /// <summary>
    /// Gets the extension of the exported file. Example: "json", "csv", "xlsx".
    /// </summary>
    public virtual string FileExtension => Format;

    /// <summary>
    /// Gets the content type of the exported file.
    /// Example: "application/json", "text/csv", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet".
    /// </summary>
    public abstract string ContentType { get; }

    /// <inheritdoc/>
    public Type ItemType => typeof(SubmissionExportRow);

    /// <inheritdoc/>
    public virtual Task<Result<FileExport>> GetHeadersAsync(ExportOptions? options, CancellationToken cancellationToken)
    {
        try
        {
            var fileName = GetFileName(options, null, FileExtension);
            var fileExport = new FileExport(ContentType, fileName);

            return Task.FromResult(Result<FileExport>.Success(fileExport));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting export headers");
            return Task.FromResult(Result<FileExport>.Error($"Failed to get export headers: {ex.Message}"));
        }
    }

    /// <inheritdoc/>
    public abstract Task<Result<FileExport>> StreamExportAsync(IAsyncEnumerable<SubmissionExportRow> records, ExportOptions? options, CancellationToken cancellationToken, PipeWriter writer);

    /// <summary>
    /// Gets the file name for the export.
    /// </summary>
    protected virtual string GetFileName(ExportOptions? options, SubmissionExportRow? firstRow, string extension)
    {
        var formId = options?.Metadata?.TryGetValue("FormId", out var value) == true
            ? Convert.ToInt64(value)
            : firstRow?.FormId;

        return formId != null
            ? $"submissions-{formId}.{extension}"
            : $"submissions.{extension}";
    }

    /// <summary>
    /// Centralized iterator that handles column discovery and JSON document lifecycle.
    /// </summary>
    protected async IAsyncEnumerable<(SubmissionExportRow Row, JsonDocument? Doc, List<ColumnDefinition<SubmissionExportRow>> Columns)>
        GetStreamContextAsync(
            IAsyncEnumerable<SubmissionExportRow> records,
            ExportOptions? options,
            [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        List<ColumnDefinition<SubmissionExportRow>>? columns = null;

        await foreach (var row in records.WithCancellation(cancellationToken))
        {
            JsonDocument? doc = null;

            if (columns == null)
            {
                // First row: initialize columns and parse the doc
                if (!string.IsNullOrWhiteSpace(row.AnswersModel))
                {
                    try
                    {
                        doc = JsonDocument.Parse(row.AnswersModel);
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogWarning(ex, "Failed to parse JSON for first row {Id}", row.Id);
                    }
                }
                columns = BuildColumns(doc, options);
            }
            else
            {
                // Subsequent rows: parse doc if model exists
                doc = string.IsNullOrWhiteSpace(row.AnswersModel) ? null : JsonDocument.Parse(row.AnswersModel);
            }

            yield return (row, doc, columns);
        }
    }

    private static List<ColumnDefinition<SubmissionExportRow>> BuildColumns(JsonDocument? doc, ExportOptions? options)
    {
        var questionNames = doc?.RootElement.EnumerateObject().Select(p => p.Name).ToList() ?? [];
        var allNames = _staticColumnAccessors.Keys.Concat(questionNames).ToList();

        var selectedNames = (options?.Columns?.Any() ?? false)
           ? options.Columns.Where(allNames.Contains)
           : allNames;

        var result = new List<ColumnDefinition<SubmissionExportRow>>();
        foreach (var name in selectedNames)
        {
            var col = _staticColumnAccessors.ContainsKey(name)
                ? (ColumnDefinition<SubmissionExportRow>)new StaticColumnDefinition<SubmissionExportRow>(name, _staticColumnAccessors[name])
                : new JsonColumnDefinition<SubmissionExportRow>(name, name);

            if (options?.Transformers is not null && options.Transformers.TryGetValue(name, out var transformer))
            {
                col.WithTransformer(transformer);
            }

            col.JsonPropertyName = JsonNamingPolicy.CamelCase.ConvertName(name);
            result.Add(col);
        }
        return result;
    }
}
