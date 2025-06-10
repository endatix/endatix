using System.Globalization;
using System.IO.Pipelines;
using System.Text.Json;
using Ardalis.GuardClauses;
using CsvHelper;
using CsvHelper.Configuration;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;

namespace Endatix.Infrastructure.Features.Submissions;

/// <summary>
/// CSV exporter for submission data, optimized for streaming and low memory usage.
/// </summary>
public sealed class SubmissionCsvExporter : IExporter<SubmissionExportRow>
{
    private const string NOT_AVAILABLE_VALUE = "N/A";
    private const string CSV_CONTENT_TYPE = "text/csv";
    private static readonly Dictionary<string, Func<SubmissionExportRow, object?>> _staticColumnAccessors = new()
    {
        [nameof(SubmissionExportRow.FormId)] = row => row.FormId,
        [nameof(SubmissionExportRow.Id)] = row => row.Id,
        [nameof(SubmissionExportRow.IsComplete)] = row => row.IsComplete,
        [nameof(SubmissionExportRow.CreatedAt)] = row => row.CreatedAt,
        [nameof(SubmissionExportRow.ModifiedAt)] = row => row.ModifiedAt,
        [nameof(SubmissionExportRow.CompletedAt)] = row => row.CompletedAt
    };

    private readonly ILogger<SubmissionCsvExporter> _logger;
    private SubmissionExportRow? _firstRow;
    private IAsyncEnumerator<SubmissionExportRow>? _enumerator;
    private readonly List<ColumnDefinition<SubmissionExportRow>> _columnDefinitions = new();

    public SubmissionCsvExporter(ILogger<SubmissionCsvExporter> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets the HTTP headers for the export without processing data.
    /// </summary>
    public Task<Result<FileExport>> GetHeadersAsync(ExportOptions? options, CancellationToken cancellationToken)
    {
        try
        {
            // For CSV export, we can determine the headers without any data processing

            // Default filename with a placeholder for form ID
            var fileName = "submissions.csv";

            // If options contains a FormId, we can use it in the filename
            if (options?.Metadata != null &&
                options.Metadata.TryGetValue("FormId", out var formIdObj))
            {
                if (formIdObj is long formId)
                {
                    fileName = $"submissions-{formId}.csv";
                }
            }

            var fileExport = new FileExport(CSV_CONTENT_TYPE, fileName);
            return Task.FromResult(Result<FileExport>.Success(fileExport));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting export headers");
            return Task.FromResult(Result<FileExport>.Error($"Failed to get export headers: {ex.Message}"));
        }
    }

    /// <summary>
    /// Streams the export directly to the provided PipeWriter.
    /// </summary>
    public async Task<Result<FileExport>> StreamExportAsync(
        IAsyncEnumerable<SubmissionExportRow> records,
        ExportOptions? options,
        CancellationToken cancellationToken,
        PipeWriter writer)
    {
        Guard.Against.Null(writer);

        try
        {
            // Initialize enumeration and read first row
            await InitializeEnumerationAsync(records, cancellationToken);

            if (_firstRow is null)
            {
                return Result<FileExport>.Success(
                    new FileExport(CSV_CONTENT_TYPE, "no-submissions.csv"));
            }

            BuildColumnDefinitions(_firstRow, options);

            // Convert PipeWriter to Stream for CsvHelper compatibility
            var writerStream = writer.AsStream();

            // Configure writer and write CSV
            var streamWriter = new StreamWriter(writerStream, leaveOpen: true);
            var csvWriter = new CsvWriter(streamWriter, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = false
            });

            await WriteHeaderRowAsync(csvWriter);
            await StreamSubmissionRowsAsync(csvWriter, cancellationToken);

            // Ensure all data is written and flushed to the pipe
            await streamWriter.FlushAsync();
            await writer.FlushAsync(cancellationToken);

            var fileName = $"submissions-{_firstRow.FormId}.csv";
            return Result<FileExport>.Success(
                new FileExport(CSV_CONTENT_TYPE, fileName));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting submissions to CSV");
            return Result<FileExport>.Error($"Failed to export submissions: {ex.Message}");
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

    /// <summary>
    /// Initializes the enumeration by creating an enumerator and reading the first row.
    /// </summary>
    private async Task InitializeEnumerationAsync(IAsyncEnumerable<SubmissionExportRow> records, CancellationToken cancellationToken)
    {
        _enumerator = records.GetAsyncEnumerator(cancellationToken);
        _firstRow = null;

        if (await _enumerator.MoveNextAsync())
        {
            _firstRow = _enumerator.Current;
        }
    }

    /// <summary>
    /// Writes the header row to the CSV file.
    /// </summary>
    private async Task WriteHeaderRowAsync(CsvWriter csv)
    {
        foreach (var column in _columnDefinitions)
        {
            csv.WriteField(column.Name);
        }

        await csv.NextRecordAsync();
    }

    /// <summary>
    /// Streams all submission rows to the CSV writer.
    /// </summary>
    private async Task StreamSubmissionRowsAsync(CsvWriter csv, CancellationToken cancellationToken)
    {
        // Define a local async generator function to yield records
        async IAsyncEnumerable<IDictionary<string, object>> GetRecords()
        {
            yield return BuildSingleCsvRecord(_firstRow!);

            while (_enumerator != null && await _enumerator.MoveNextAsync())
            {
                yield return BuildSingleCsvRecord(_enumerator.Current);
            }
        }

        // Write all records to the CSV
        await csv.WriteRecordsAsync(GetRecords(), cancellationToken);
    }

    /// <summary>
    /// Builds a dictionary representing a CSV record by parsing JSON once per row.
    /// </summary>
    private IDictionary<string, object> BuildSingleCsvRecord(SubmissionExportRow row)
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