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
public abstract class SubmissionExporterBase : IExporter<SubmissionExportRow>
{
    protected const string NOT_AVAILABLE_VALUE = "N/A";
    protected readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubmissionExporterBase"/> class.
    /// </summary>
    /// <param name="logger">The logger to use for the exporter.</param>
    protected SubmissionExporterBase(ILogger logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public abstract string Format { get; }

    /// <inheritdoc/>
    public Type ItemType => typeof(SubmissionExportRow);

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
    /// <param name="options">The export options.</param>
    /// <param name="firstRow">The first row of the export.</param>
    /// <param name="extension">The extension of the export.</param>
    /// <returns>The file name for the export.</returns>
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
    /// Prepares the records for export by building the column definitions and building the records.
    /// </summary>
    /// <param name="records">The records to prepare.</param>
    /// <param name="options">The export options.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>An asynchronous enumerable of dictionaries representing the records.</returns>
    protected async IAsyncEnumerable<IDictionary<string, object?>> PrepareRecordsAsync(
        IAsyncEnumerable<SubmissionExportRow> records,
        ExportOptions? options,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        List<ColumnDefinition<SubmissionExportRow>>? columns = null;

        await foreach (var row in records.WithCancellation(cancellationToken))
        {
            columns ??= BuildColumns(row, options).ToList();
            yield return BuildSingleRecord(row, columns);
        }
    }

    /// <summary>
    /// Builds the column definitions based on entity columns, question names, and options.
    /// </summary>
    /// <param name="row">The first row of the export.</param>
    /// <param name="options">The export options.</param>
    protected virtual IEnumerable<ColumnDefinition<SubmissionExportRow>> BuildColumns(SubmissionExportRow row, ExportOptions? options)
    {
        var questionNames = ExtractQuestionNames(row);
        List<string> allColumnNames = [.. _staticColumnAccessors.Keys, .. questionNames];

        var selectedNames = (options?.Columns?.Any() == true)
           ? options.Columns.Where(allColumnNames.Contains)
           : allColumnNames;

        foreach (var name in selectedNames)
        {
            ColumnDefinition<SubmissionExportRow> col = _staticColumnAccessors.ContainsKey(name)
                ? new StaticColumnDefinition<SubmissionExportRow>(name, _staticColumnAccessors[name])
                : new JsonColumnDefinition<SubmissionExportRow>(name, name);

            if (options?.Transformers?.TryGetValue(name, out var transformer) == true)
            {
                col.WithTransformer(transformer);
            }
            yield return col;
        }
    }

    /// <summary>
    /// Builds a dictionary representing a single record by parsing JSON once per row.
    /// </summary>
    /// <param name="row">The row to build the record for.</param>
    /// <param name="columns">The columns definitions to build the record for.</param>
    /// <returns>A dictionary representing the record.</returns>
    protected IDictionary<string, object?> BuildSingleRecord(SubmissionExportRow row, IEnumerable<ColumnDefinition<SubmissionExportRow>> columns)
    {
        var record = new Dictionary<string, object?>();
        try
        {
            using var document = string.IsNullOrWhiteSpace(row.AnswersModel) ? null : JsonDocument.Parse(row.AnswersModel);
            foreach (var columnDef in columns)
            {
                try
                {
                    record[columnDef.Name] = columnDef.GetFormattedValue(row, document);
                }
                catch (JsonException ex)
                {
                    _logger.LogWarning(ex, "Failed to parse JSON answers for row ID {RowId}. Form ID: {FormId}.", row.Id, row.FormId);
                }
            }
            return record;
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON answers for row ID {RowId}. Form ID: {FormId}.",
                row.Id, row.FormId);

            // On parsing error, still include static columns
            foreach (var columnDef in columns)
            {
                if (columnDef is StaticColumnDefinition<SubmissionExportRow>)
                {
                    record[columnDef.Name] = columnDef.GetFormattedValue(row, null);
                }
                else
                {
                    record[columnDef.Name] = NOT_AVAILABLE_VALUE;
                }
            }
            return record;
        }
    }

    /// <summary>
    /// Extracts the question names from the answers model.
    /// </summary>
    /// <param name="row">The row to extract the question names from.</param>
    /// <returns>The question names.</returns>
    private List<string> ExtractQuestionNames(SubmissionExportRow row)
    {
        if (string.IsNullOrWhiteSpace(row.AnswersModel))
        {
            return [];
        }

        try
        {
            using var doc = JsonDocument.Parse(row.AnswersModel);
            return doc.RootElement.EnumerateObject().Select(p => p.Name).ToList();
        }
        catch (JsonException) { return []; }
    }

    private static readonly Dictionary<string, Func<SubmissionExportRow, object?>> _staticColumnAccessors = new()
    {
        [nameof(SubmissionExportRow.FormId)] = row => row.FormId,
        [nameof(SubmissionExportRow.Id)] = row => row.Id,
        [nameof(SubmissionExportRow.IsComplete)] = row => row.IsComplete,
        [nameof(SubmissionExportRow.CreatedAt)] = row => row.CreatedAt,
        [nameof(SubmissionExportRow.ModifiedAt)] = row => row.ModifiedAt,
        [nameof(SubmissionExportRow.CompletedAt)] = row => row.CompletedAt
    };
}