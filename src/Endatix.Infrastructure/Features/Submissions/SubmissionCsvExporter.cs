using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
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
        if (outputStream is null)
        {
            throw new ArgumentNullException(nameof(outputStream), "Output stream must be provided for streaming export.");
        }

        // --- Formatting: extract headers and dynamic columns ---
        var (firstRow, orderedKeys, dynamicColumns, enumerator) = await ExtractHeadersAndColumns(records, options, cancellationToken);

        var writer = new StreamWriter(outputStream, leaveOpen: true);
        var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false
        });

        // Write header
        foreach (var key in orderedKeys)
        {
            csv.WriteField(key);
        }

        await csv.NextRecordAsync();

        // If there are no rows, just flush and return
        if (firstRow is null)
        {
            await writer.FlushAsync();
            return new ExportFileResult("text/csv", "submissions-export.csv");
        }

        // --- Export logic: stream records ---
        async IAsyncEnumerable<IDictionary<string, object>> GetRecords()
        {
            yield return BuildRecord(firstRow, dynamicColumns, options?.Transformers);
            while (await enumerator.MoveNextAsync())
            {
                yield return BuildRecord(enumerator.Current, dynamicColumns, options?.Transformers);
            }
        }

        await csv.WriteRecordsAsync(GetRecords(), cancellationToken);
        await writer.FlushAsync();

        var fileName = $"submissions-{firstRow.FormId}.csv";
        return new ExportFileResult("text/csv", fileName);
    }

    /// <summary>
    /// Extracts the first row, ordered columns (entity + dynamic), and dynamic columns from the records.
    /// Respects column selection from options if provided.
    /// Returns the enumerator positioned after the first row.
    /// </summary>
    private static async Task<(SubmissionExportRow? firstRow, List<string> orderedKeys, List<string> dynamicColumns, IAsyncEnumerator<SubmissionExportRow> enumerator)>
        ExtractHeadersAndColumns(
            IAsyncEnumerable<SubmissionExportRow> records, 
            ExportOptions options, 
            CancellationToken cancellationToken)
    {
        // Define default entity columns
        var entityColumns = new List<string>
        {
            nameof(SubmissionExportRow.FormId),
            nameof(SubmissionExportRow.Id),
            nameof(SubmissionExportRow.IsComplete),
            nameof(SubmissionExportRow.CreatedAt),
            nameof(SubmissionExportRow.ModifiedAt),
            nameof(SubmissionExportRow.CompletedAt)
        };

        var enumerator = records.GetAsyncEnumerator(cancellationToken);
        SubmissionExportRow? firstRow = null;
        if (await enumerator.MoveNextAsync())
        {
            firstRow = enumerator.Current;
        }

        // Determine dynamic columns from the first row
        var dynamicColumns = new List<string>();
        if (firstRow != null && !string.IsNullOrWhiteSpace(firstRow.AnswersModel))
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(firstRow.AnswersModel);
            if (dict is not null)
            {
                dynamicColumns = new List<string>(dict.Keys);
            }
        }

        // Combine all columns
        var allColumns = entityColumns.Concat(dynamicColumns).ToList();
        
        // Filter by options.Columns if provided
        List<string> orderedKeys = allColumns;
        if (options?.Columns != null && options.Columns.Any())
        {
            // Only include columns that exist in the data
            orderedKeys = options.Columns
                .Where(allColumns.Contains)
                .ToList();
        }

        return (firstRow, orderedKeys, dynamicColumns, enumerator);
    }

    /// <summary>
    /// Builds a dictionary representing a CSV record from a submission row.
    /// Applies transformers if provided in options.
    /// </summary>
    private static IDictionary<string, object> BuildRecord(
        SubmissionExportRow row,
        List<string> dynamicColumns,
        IDictionary<string, System.Func<object, string>> transformers = null)
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