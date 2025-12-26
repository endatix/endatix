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
public sealed class SubmissionJsonExporter : IExporter<SubmissionExportRow>
{
    private const string JSON_CONTENT_TYPE = "application/json";

    private const string NOT_AVAILABLE_VALUE = "N/A";

    /// <inheritdoc/>
    public string Format => "json";

    /// <inheritdoc/>
    public Type ItemType => typeof(SubmissionExportRow);

    private static readonly Dictionary<string, Func<SubmissionExportRow, object?>> _staticColumnAccessors = new()
    {
        [nameof(SubmissionExportRow.FormId)] = row => row.FormId,
        [nameof(SubmissionExportRow.Id)] = row => row.Id,
        [nameof(SubmissionExportRow.IsComplete)] = row => row.IsComplete,
        [nameof(SubmissionExportRow.CreatedAt)] = row => row.CreatedAt,
        [nameof(SubmissionExportRow.ModifiedAt)] = row => row.ModifiedAt,
        [nameof(SubmissionExportRow.CompletedAt)] = row => row.CompletedAt
    };

    private readonly ILogger<SubmissionJsonExporter> _logger;
    private readonly List<ColumnDefinition<SubmissionExportRow>> _columnDefinitions = new();

    public SubmissionJsonExporter(ILogger<SubmissionJsonExporter> logger)
    {
        _logger = logger;
    }

    // <inheritdoc/>
    public Task<Result<FileExport>> GetHeadersAsync(ExportOptions? options, CancellationToken cancellationToken)
    {
        try
        {
            var fileName = "submissions.json";
            var fileExport = new FileExport(JSON_CONTENT_TYPE, fileName);
            return Task.FromResult(Result<FileExport>.Success(fileExport));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting export headers");
            return Task.FromResult(Result<FileExport>.Error($"Failed to get export headers: {ex.Message}"));
        }
    }

    public async Task<Result<FileExport>> StreamExportAsync(IAsyncEnumerable<SubmissionExportRow> records, ExportOptions? options, CancellationToken cancellationToken, PipeWriter writer)
    {
        try
        {
            using var stream = writer.AsStream();

            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            // We stream the TRANSFORMED records (dictionaries), not the raw rows
            var processedRecords = PrepareRecordsAsync(records, options, cancellationToken);

            await JsonSerializer.SerializeAsync(
                stream,
                processedRecords,
                jsonOptions,
                cancellationToken);

            await writer.FlushAsync(cancellationToken);

            var fileExport = new FileExport(
                fileName: $"submissions-{options?.Metadata?["FormId"]}.json",
                contentType: JSON_CONTENT_TYPE);

            return Result<FileExport>.Success(fileExport);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting submissions to JSON");
            return Result<FileExport>.Error($"Failed to export submissions: {ex.Message}");
        }
    }

    private async IAsyncEnumerable<IDictionary<string, object>> PrepareRecordsAsync(IAsyncEnumerable<SubmissionExportRow> records, ExportOptions? options, CancellationToken cancellationToken)
    {
        await foreach (var row in records.WithCancellation(cancellationToken))
        {
            // Initialize columns based on the first row if not already done
            if (_columnDefinitions.Count == 0)
            {
                BuildColumnDefinitions(row, options);
            }

            yield return BuildSingleRecord(row);
        }
    }

    /// <summary>
    /// Builds the column definitions based on entity columns, question names, and options.
    /// </summary>
    private void BuildColumnDefinitions(SubmissionExportRow firstRow, ExportOptions? options)
    {
        var questionNames = new List<string>();

        // Extract question names from first row
        if (!string.IsNullOrWhiteSpace(firstRow.AnswersModel))
        {
            try
            {
                using var document = JsonDocument.Parse(firstRow.AnswersModel);
                var root = document.RootElement;

                foreach (var property in root.EnumerateObject())
                {
                    questionNames.Add(property.Name);
                }
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Failed to parse JSON answers for row ID {RowId}. Form ID: {FormId}. This may cause missing columns in export.",
                    firstRow.Id, firstRow.FormId);
            }
        }

        var allColumnNames = new List<string>(_staticColumnAccessors.Keys);
        allColumnNames.AddRange(questionNames);

        // Apply column filters if specified
        var columnNames = (options?.Columns != null && options.Columns.Any())
            ? options.Columns
                .Where(allColumnNames.Contains)
                .ToList()
            : allColumnNames;

        // Add entity columns that are in the final selection
        foreach (var name in columnNames.Where(_staticColumnAccessors.ContainsKey))
        {
            var columnDef = new StaticColumnDefinition<SubmissionExportRow>(
                name,
                _staticColumnAccessors[name]
            );

            // Add transformer if specified
            if (options?.Transformers != null && options.Transformers.TryGetValue(name, out var transformer))
            {
                _ = columnDef.WithTransformer(transformer);
            }

            _columnDefinitions.Add(columnDef);
        }

        // Add dynamic columns from question names
        foreach (var questionName in columnNames.Where(n => questionNames.Contains(n)))
        {
            var columnDef = new JsonColumnDefinition<SubmissionExportRow>(
                questionName,
                questionName
            );

            // Add transformer if specified
            if (options?.Transformers != null && options.Transformers.TryGetValue(questionName, out var transformer))
            {
                _ = columnDef.WithTransformer(transformer);
            }

            _columnDefinitions.Add(columnDef);
        }
    }

    private IDictionary<string, object> BuildSingleRecord(SubmissionExportRow row)
    {
        var record = new Dictionary<string, object>();
        JsonDocument? document = null;

        try
        {
            if (!string.IsNullOrWhiteSpace(row.AnswersModel))
            {
                document = JsonDocument.Parse(row.AnswersModel);
            }

            // Process each column with the cached document
            foreach (var columnDef in _columnDefinitions)
            {
                record[columnDef.Name] = columnDef.GetFormattedValue(row, document);
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse JSON answers for row ID {RowId}. Form ID: {FormId}.",
                row.Id, row.FormId);

            // On parsing error, still include static columns
            foreach (var columnDef in _columnDefinitions)
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
        }
        finally
        {
            document?.Dispose();
        }

        return record;
    }
}
