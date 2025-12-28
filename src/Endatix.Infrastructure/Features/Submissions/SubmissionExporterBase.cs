using System.IO.Pipelines;
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
        long? formId = null;
        if (options?.Metadata?.TryGetValue("FormId", out var value) == true)
        {
            formId = value as long?;
        }

        formId ??= firstRow?.FormId;

        return formId != null
            ? $"submissions-{formId}.{extension}"
            : $"submissions.{extension}";
    }

    /// <summary>
    /// Initializes columns and returns the pre-parsed JsonDocument for the first row to avoid double parsing.
    /// </summary>
    protected (List<ColumnDefinition<SubmissionExportRow>> Columns, JsonDocument? FirstRowDoc) GetInitialContext(SubmissionExportRow firstRow, ExportOptions? options)
    {
        JsonDocument? firstRowDoc = null;
        if (!string.IsNullOrWhiteSpace(firstRow.AnswersModel))
        {
            try
            {
                firstRowDoc = JsonDocument.Parse(firstRow.AnswersModel);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse JSON for first row {Id}", firstRow.Id);
            }
        }

        var columns = BuildColumnsFromDoc(firstRowDoc, options).ToList();
        return (columns, firstRowDoc);
    }

    private IEnumerable<ColumnDefinition<SubmissionExportRow>> BuildColumnsFromDoc(JsonDocument? doc, ExportOptions? options)
    {
        List<string> questionNames = [];
        if (doc is not null)
        {
            questionNames = doc.RootElement.EnumerateObject()
                .Select(p => p.Name)
                .ToList();
        }

        var allNames = _staticColumnAccessors.Keys.Concat(questionNames).ToList();

        var selectedNames = (options?.Columns?.Any() == true)
           ? options.Columns.Where(allNames.Contains)
           : allNames;

        foreach (var name in selectedNames)
        {
            var col = _staticColumnAccessors.ContainsKey(name)
                ? (ColumnDefinition<SubmissionExportRow>)new StaticColumnDefinition<SubmissionExportRow>(name, _staticColumnAccessors[name])
                : new JsonColumnDefinition<SubmissionExportRow>(name, name);

            if (options?.Transformers?.TryGetValue(name, out var transformer) == true)
            {
                col.WithTransformer(transformer);
            }

            yield return col;
        }
    }
}
