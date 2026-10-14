using System.IO.Pipelines;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Exporting.ColumnDefinitions;
using Endatix.Infrastructure.Exporting.Formatters;
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

    // The second argument is what a missing value is written as: see MissingSystemValue.
    private static readonly Dictionary<string, Func<SubmissionExportRow, string, object?>> _staticColumnAccessors = new()
    {
        [SubmissionExportRow.SystemColumns.FormId] = (row, _) => row.FormId,
        [SubmissionExportRow.SystemColumns.Id] = (row, _) => row.Id,
        [SubmissionExportRow.SystemColumns.IsComplete] = (row, _) => row.IsComplete,
        [SubmissionExportRow.SystemColumns.CollectionStatus] = (row, _) => row.CollectionStatus,
        [SubmissionExportRow.SystemColumns.CreatedAt] = (row, _) => row.CreatedAt,
        [SubmissionExportRow.SystemColumns.ModifiedAt] = (row, _) => row.ModifiedAt,
        [SubmissionExportRow.SystemColumns.StartedAt] = (row, missing) => row.StartedAt is null ? missing : row.StartedAt,
        [SubmissionExportRow.SystemColumns.CompletedAt] = (row, missing) => row.CompletedAt is null ? missing : row.CompletedAt,
        [SubmissionExportRow.SystemColumns.DurationSeconds] = GetDurationSecondsExportValue,
        [SubmissionExportRow.SystemColumns.SubmitterId] = (row, missing) => row.SubmitterId is null ? missing : row.SubmitterId,
        [SubmissionExportRow.SystemColumns.SubmitterDisplayId] = (row, missing) => row.SubmitterDisplayId ?? missing
    };

    private static object GetDurationSecondsExportValue(SubmissionExportRow row, string missing)
    {
        if (!row.IsComplete)
        {
            return missing;
        }

        var durationSeconds = SubmissionExportRow.CalculateDurationSeconds(row.StartedAt, row.CompletedAt);
        if (durationSeconds is null)
        {
            return missing;
        }

        return durationSeconds.Value;
    }

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
            return Task.FromResult(Result<FileExport>.Error("Failed to get export headers."));
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
    /// Booleans render as codebook category ids (<c>1</c>/<c>0</c>) for Shoji and for reporting
    /// runs that ask for it; every other format renders them as <c>true</c>/<c>false</c>.
    /// </summary>
    protected bool UsesCategoryIdBooleans(ExportOptions? options) =>
        string.Equals(Format, "csv-shoji", StringComparison.OrdinalIgnoreCase) ||
        (options?.Metadata is not null &&
         options.Metadata.TryGetValue(SubmissionExportMetadataKeys.ExecutionSettings, out var settings) &&
         settings is SubmissionExportExecutionSettings executionSettings &&
         executionSettings.EncodeBooleansAsCategoryIds);

    /// <summary>
    /// What a missing system value is written as. Crunch rejects any text in a numeric or datetime column, so a
    /// Crunch-shaped export leaves the cell empty; every other export writes <c>N/A</c>.
    /// </summary>
    protected string MissingSystemValue(ExportOptions? options) =>
        UsesCategoryIdBooleans(options) ? string.Empty : NOT_AVAILABLE_VALUE;

    /// <summary>
    /// Yields (row, answers doc, columns) for streaming export.
    /// </summary>
    protected async IAsyncEnumerable<(SubmissionExportRow Row, JsonDocument? Doc, List<ColumnDefinition<SubmissionExportRow>> Columns)>
        GetStreamContextAsync(
            IAsyncEnumerable<SubmissionExportRow> records,
            ExportOptions? options,
            [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var columns = TryBuildColumnsFromPlan(options);

        await foreach (var row in records.WithCancellation(cancellationToken))
        {
            var doc = TryParseAnswersJson(row.AnswersModel, row.Id, _logger);

            if (columns is null)
            {
                columns = BuildColumns(doc, options);
            }

            yield return (row, doc, columns);
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

    private List<ColumnDefinition<SubmissionExportRow>>? TryBuildColumnsFromPlan(ExportOptions? options)
    {
        if (options?.Metadata is null ||
            !options.Metadata.TryGetValue(SubmissionExportMetadataKeys.ColumnPlan, out var planObject) ||
            planObject is not SubmissionExportColumnPlan plan)
        {
            return null;
        }

        List<ColumnDefinition<SubmissionExportRow>> columns = [];
        foreach (var entry in plan.Columns)
        {
            var column = entry.Source == SubmissionExportColumnSources.System
                ? BuildStaticColumn(entry.ExportKey, options)
                : BuildJsonColumn(entry.ExportKey, entry.CanonicalKey);

            if (options.Formatters is not null && options.Formatters.TryGetValue(column.Name, out var formatter))
            {
                column.SetFormatter(new DelegateFormatter(formatter));
            }

            column.JsonPropertyName = JsonNamingPolicy.CamelCase.ConvertName(entry.ExportKey);
            columns.Add(column);
        }

        return columns;
    }

    private StaticColumnDefinition<SubmissionExportRow> BuildStaticColumn(string exportKey, ExportOptions? options)
    {
        if (!_staticColumnAccessors.TryGetValue(exportKey, out var accessor))
        {
            throw new InvalidOperationException($"Unsupported system export column '{exportKey}'.");
        }

        var missing = MissingSystemValue(options);
        return new StaticColumnDefinition<SubmissionExportRow>(exportKey, row => accessor(row, missing));
    }

    private ColumnDefinition<SubmissionExportRow> BuildJsonColumn(string exportKey, string canonicalKey)
    {
        JsonColumnDefinition<SubmissionExportRow> jsonColumn = new(exportKey, canonicalKey);
        foreach (var transformer in _globalTransformers)
        {
            jsonColumn.AddTransformer(transformer);
        }

        return jsonColumn;
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
            var column = BuildColumnDefinition(name, options);

            if (options?.Formatters is not null && options.Formatters.TryGetValue(column.Name, out var formatter))
            {
                column.SetFormatter(new DelegateFormatter(formatter));
            }

            column.JsonPropertyName = JsonNamingPolicy.CamelCase.ConvertName(name);
            result.Add(column);
        }
        return result;
    }

    private ColumnDefinition<SubmissionExportRow> BuildColumnDefinition(string name, ExportOptions? options)
    {
        if (_staticColumnAccessors.ContainsKey(name))
        {
            return BuildStaticColumn(name, options);
        }

        var jsonColumn = new JsonColumnDefinition<SubmissionExportRow>(name, name);
        foreach (var transformer in _globalTransformers)
        {
            jsonColumn.AddTransformer(transformer);
        }

        return jsonColumn;
    }
}
