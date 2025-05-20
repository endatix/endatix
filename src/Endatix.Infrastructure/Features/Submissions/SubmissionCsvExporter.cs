using System.Globalization;
using System.Text.Json;
using Ardalis.GuardClauses;
using CsvHelper;
using CsvHelper.Configuration;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;

namespace Endatix.Infrastructure.Features.Submissions;

/// <summary>
/// CSV exporter for submission data, optimized for streaming and low memory usage.
/// </summary>
public sealed class SubmissionCsvExporter : IExporter<SubmissionExportRow>
{
    /// <summary>
    /// Streams the export directly to the provided output stream.
    /// </summary>
    public async Task<ExportFileResult> StreamExportAsync(
        IAsyncEnumerable<SubmissionExportRow> records,
        ExportOptions options,
        CancellationToken cancellationToken,
        Stream outputStream)
    {
        Guard.Against.Null(outputStream);

        var (firstRow, enumerator) = await PeekFirstRowAsync(records, cancellationToken);

        if (firstRow is null)
        {
            return new ExportFileResult("text/csv", "no-submissions.csv");
        }

        var writer = new StreamWriter(outputStream, leaveOpen: true);
        var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false
        });

        // Extract column information
        var questionNames = ExtractQuestionNames(firstRow);
        var headerColumns = BuildHeaderColumns(questionNames, options);

        await WriteHeaderRowAsync(csv, headerColumns);
        await StreamDataRowsAsync(csv, firstRow, enumerator, questionNames, options, cancellationToken);

        // Write all data to the output stream
        await writer.FlushAsync();
        var fileName = firstRow is not null
            ? $"submissions-{firstRow.FormId}.csv"
            : "submissions-export.csv";
        return new ExportFileResult("text/csv", fileName);
    }

    /// <summary>
    /// Peeks at the first row to get column information while preserving the enumerator position.
    /// </summary>
    private static async Task<(SubmissionExportRow? FirstRow, IAsyncEnumerator<SubmissionExportRow> Enumerator)>
        PeekFirstRowAsync(IAsyncEnumerable<SubmissionExportRow> records, CancellationToken cancellationToken)
    {
        var enumerator = records.GetAsyncEnumerator(cancellationToken);
        SubmissionExportRow? firstRow = null;

        if (await enumerator.MoveNextAsync())
        {
            firstRow = enumerator.Current;
        }

        return (firstRow, enumerator);
    }

    /// <summary>
    /// Extracts the question names from the first row's answers model.
    /// </summary>
    private static List<string> ExtractQuestionNames(SubmissionExportRow? firstRow)
    {
        var questionNames = new List<string>();

        if (firstRow != null && !string.IsNullOrWhiteSpace(firstRow.AnswersModel))
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(firstRow.AnswersModel);
            if (dict is not null)
            {
                questionNames = [.. dict.Keys];
            }
        }

        return questionNames;
    }

    /// <summary>
    /// Gets the standard entity columns that are always included.
    /// </summary>
    private static List<string> GetEntityColumns()
    {
        return new List<string>
        {
            nameof(SubmissionExportRow.FormId),
            nameof(SubmissionExportRow.Id),
            nameof(SubmissionExportRow.IsComplete),
            nameof(SubmissionExportRow.CreatedAt),
            nameof(SubmissionExportRow.ModifiedAt),
            nameof(SubmissionExportRow.CompletedAt)
        };
    }

    /// <summary>
    /// Builds the final set of header columns based on entity columns, dynamic columns, and options.
    /// </summary>
    private static List<string> BuildHeaderColumns(List<string> dynamicColumns, ExportOptions? options)
    {
        // Combine all columns
        var allColumns = GetEntityColumns().Concat(dynamicColumns).ToList();

        // Filter by options.Columns if provided
        if (options?.Columns != null && options.Columns.Any())
        {
            // Only include columns that exist in the data
            return options.Columns
                .Where(allColumns.Contains)
                .ToList();
        }

        return allColumns;
    }

    /// <summary>
    /// Writes the header row to the CSV file.
    /// </summary>
    private static async Task WriteHeaderRowAsync(CsvWriter csv, List<string> headerColumns)
    {
        foreach (var column in headerColumns)
        {
            csv.WriteField(column);
        }

        await csv.NextRecordAsync();
    }

    /// <summary>
    /// Streams all data rows to the CSV writer.
    /// </summary>
    private static async Task StreamDataRowsAsync(
        CsvWriter csv,
        SubmissionExportRow? firstRow,
        IAsyncEnumerator<SubmissionExportRow> enumerator,
        List<string> questionNames,
        ExportOptions? options,
        CancellationToken cancellationToken)
    {
        // If there are no rows, just return
        if (firstRow is null)
        {
            return;
        }

        // Define a local async generator function to yield records
        async IAsyncEnumerable<IDictionary<string, object>> GetRecords()
        {
            // First yield the first row we already read
            yield return BuildRecord(firstRow, questionNames, options?.Transformers);

            // Then yield the rest of the rows
            while (await enumerator.MoveNextAsync())
            {
                yield return BuildRecord(enumerator.Current, questionNames, options?.Transformers);
            }
        }

        // Write all records to the CSV
        await csv.WriteRecordsAsync(GetRecords(), cancellationToken);
    }

    /// <summary>
    /// Builds a dictionary representing a CSV record from a submission row.
    /// Applies transformers if provided in options.        
    /// </summary>
    private static IDictionary<string, object> BuildRecord(
        SubmissionExportRow row,
        List<string> dynamicColumns,
        IDictionary<string, System.Func<object, string>>? transformers = null)
    {
        var record = new Dictionary<string, object>
        {
            [nameof(SubmissionExportRow.FormId)] = row.FormId,
            [nameof(SubmissionExportRow.Id)] = row.Id,
            [nameof(SubmissionExportRow.IsComplete)] = row.IsComplete,
            [nameof(SubmissionExportRow.CreatedAt)] = row.CreatedAt.ToString("o"),
            [nameof(SubmissionExportRow.ModifiedAt)] = row.ModifiedAt?.ToString("o") ?? string.Empty,
            [nameof(SubmissionExportRow.CompletedAt)] = row.CompletedAt?.ToString("o") ?? string.Empty
        };

        if (!string.IsNullOrWhiteSpace(row.AnswersModel))
        {
            using var doc = JsonDocument.Parse(row.AnswersModel);
            var root = doc.RootElement;
            foreach (var key in dynamicColumns)
            {
                var value = string.Empty;
                if (root.TryGetProperty(key, out var element))
                {
                    value = element.ValueKind switch
                    {
                        JsonValueKind.String => element.GetString(),
                        JsonValueKind.Number => element.ToString(),
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        JsonValueKind.Null => string.Empty,
                        _ => element.ToString()
                    } ?? string.Empty;
                }

                // Apply transformer if available
                if (transformers != null && transformers.TryGetValue(key, out var transformer))
                {
                    value = transformer(value);
                }

                record[key] = value;
            }
        }
        else
        {
            foreach (var key in dynamicColumns)
            {
                record[key] = string.Empty;
            }
        }

        return record;
    }
}