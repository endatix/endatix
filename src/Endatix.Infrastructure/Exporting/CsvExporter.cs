using System.Globalization;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;

namespace Endatix.Infrastructure.Exporting;

/// <summary>
/// CSV exporter implementation using CsvHelper, optimized for streaming and low memory usage.
/// </summary>
public sealed class CsvExporter : IExporter
{
    /// <summary>
    /// Streams the export directly to the provided output stream. Returns an ExportFileResult with file name and content type only.
    /// </summary>
    public async Task<ExportFileResult> StreamExportAsync<T>(
        IAsyncEnumerable<T> records,
        IEnumerable<string> columns,
        string format,
        CancellationToken cancellationToken,
        Stream outputStream = null)
    {
        if (format.ToLowerInvariant() != "csv")
        {
            throw new NotSupportedException($"Format '{format}' is not supported by CsvExporter.");
        }

        if (typeof(T) != typeof(SubmissionExportRow))
        {
            throw new NotSupportedException($"CsvExporter only supports SubmissionExportRow for now.");
        }

        if (outputStream is null)
        {
            throw new ArgumentNullException(nameof(outputStream), "Output stream must be provided for streaming export.");
        }

        var entityColumns = new List<string>
        {
            nameof(SubmissionExportRow.FormId),
            nameof(SubmissionExportRow.Id),
            nameof(SubmissionExportRow.IsComplete),
            nameof(SubmissionExportRow.CreatedAt),
            nameof(SubmissionExportRow.ModifiedAt),
            nameof(SubmissionExportRow.CompletedAt)
        };

        // Buffer only the first row
        await using var enumerator = (records as IAsyncEnumerable<SubmissionExportRow>)?.GetAsyncEnumerator(cancellationToken)
            ?? throw new NotSupportedException($"CsvExporter only supports IAsyncEnumerable<SubmissionExportRow> for now.");
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
        var orderedKeys = entityColumns;
        if (dynamicColumns.Count > 0)
        {
            orderedKeys = new List<string>(entityColumns.Concat(dynamicColumns));
        }

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
            return new ExportFileResult(Array.Empty<byte>(), "text/csv", "submissions-export.csv");
        }

        // Build an async enumerable of records for CSV streaming
        async IAsyncEnumerable<IDictionary<string, object>> GetRecords()
        {
            yield return BuildRecord(firstRow, dynamicColumns);
            while (await enumerator.MoveNextAsync())
            {
                yield return BuildRecord(enumerator.Current, dynamicColumns);
            }
        }

        await csv.WriteRecordsAsync(GetRecords(), cancellationToken);
        await writer.FlushAsync();

        var fileName = $"submissions-{firstRow.FormId}.csv";
        return new ExportFileResult(Array.Empty<byte>(), "text/csv", fileName);
    }

    private static IDictionary<string, object> BuildRecord(
        SubmissionExportRow row,
        List<string> dynamicColumns)
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