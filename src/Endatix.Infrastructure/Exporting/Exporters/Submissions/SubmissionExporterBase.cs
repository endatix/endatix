using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Exporting.ColumnDefinitions;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Exporting.Exporters.Submissions;

/// <summary>
/// Base class for submission exporters to reuse common logic for streaming and header generation.
/// </summary>
public abstract class SubmissionExporterBase(
    ILogger logger,
    IEnumerable<IValueTransformer> globalTransformers) : IExporter<SubmissionExportRow>
{
    protected const string NOT_AVAILABLE_VALUE = "N/A";

    protected readonly ILogger _logger = logger;
    private readonly List<IValueTransformer> _globalTransformers = globalTransformers?.ToList() ?? [];

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

    /// <inheritdoc/>
    public Type ItemType => typeof(SubmissionExportRow);

    /// <inheritdoc/>
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
    protected virtual string GetFileName(ExportOptions? options, SubmissionExportRow? firstRow, string extension)
    {
        var formId = options?.Metadata?.TryGetValue("FormId", out var value) == true
            ? Convert.ToInt64(value)
            : firstRow?.FormId;

        return formId is not null
            ? $"submissions-{formId}.{extension}"
            : $"submissions.{extension}";
    }

    /// <summary>
    /// Yields (row, answers doc, columns) for streaming export.
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
            var isFirstRow = columns is null;
            var doc = TryParseAnswersJson(row.AnswersModel, row.Id, _logger);

            if (isFirstRow)
            {
                columns = BuildColumns(doc, options);
            }

            yield return (row, doc, columns!);
        }
    }

    private static JsonDocument? TryParseAnswersJson(string? answersJson, long rowId, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(answersJson))
        {
            return null;
        }

        try
        {
            return JsonDocument.Parse(answersJson);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to parse answers JSON for row {Id}", rowId);
            return null;
        }
    }

    private List<ColumnDefinition<SubmissionExportRow>> BuildColumns(JsonDocument? doc, ExportOptions? options)
    {
        var questionNames = doc?.RootElement.EnumerateObject().Select(p => p.Name).ToList() ?? [];
        var allNames = _staticColumnAccessors.Keys.Concat(questionNames).ToList();

        var selectedNames = (options?.Columns?.Any() ?? false)
            ? options.Columns.Where(allNames.Contains)
            : allNames;

        var result = new List<ColumnDefinition<SubmissionExportRow>>();
        foreach (var name in selectedNames)
        {
            var column = BuildColumnDefinition(name);

            if (options?.Formatters is not null && options.Formatters.TryGetValue(name, out var transformer))
            {
                column.WithFormatter(transformer);
            }

            column.JsonPropertyName = JsonNamingPolicy.CamelCase.ConvertName(name);
            result.Add(column);
        }
        return result;
    }

    private ColumnDefinition<SubmissionExportRow> BuildColumnDefinition(string name)
    {
        if (_staticColumnAccessors.ContainsKey(name))
        {
            return new StaticColumnDefinition<SubmissionExportRow>(name, _staticColumnAccessors[name]);
        }

        var jsonColumn = new JsonColumnDefinition<SubmissionExportRow>(name, name);
        foreach (var transformer in _globalTransformers)
        {
            jsonColumn.AddTransformer(transformer);
        }

        return jsonColumn;
    }
}
