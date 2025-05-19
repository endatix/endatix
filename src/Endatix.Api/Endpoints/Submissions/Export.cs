using System.Globalization;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Endatix.Framework.Tooling;
using Endatix.Core.Entities;
using Endatix.Core.Abstractions.Repositories;

namespace Endatix.Api.Endpoints.Submissions;

public class Export(ISubmissionExportRepository exportRepository) : Endpoint<Export.Request>
{
    public class Request
    {
        public long FormId { get; set; }
    }

    public override void Configure()
    {
        Get("forms/{formId}/submissions/export");
        AllowAnonymous(); // Adjust as needed for auth
        Summary(s =>
       {
           s.Summary = "Export submissions";
           s.Description = "Export submissions for a given form";
           s.Responses[200] = "The submissions were successfully exported";
           s.Responses[400] = "Invalid input data.";
           s.Responses[404] = "Form not found. Cannot export submissions";
       });
    }

    public override async Task HandleAsync(Request req, CancellationToken ct)
    {
        using var measureExecution = new MeasureExecution();

        try
        {
            var exportRows = GetExportDataAsync(req.FormId, ct);
            HttpContext.Response.ContentType = "text/csv";
            HttpContext.Response.Headers.ContentDisposition = $"attachment; filename=submissions-{req.FormId}.csv";

            await ExportToCsvAsync(exportRows, HttpContext.Response.Body, ct);
        }
        catch (Exception ex)
        {
            // Log the error (replace with your logger if available)
            Console.WriteLine($"Export failed: {ex.Message}\n{ex.StackTrace}");

            // If the response has not started, you can set a 500 status code and write a message
            if (!HttpContext.Response.HasStarted)
            {
                HttpContext.Response.StatusCode = 500;
                await HttpContext.Response.WriteAsync("An error occurred during export.");
            }
            // If the response has started, you can't change the status code or write more data
        }
    }

    private IAsyncEnumerable<SubmissionExportRow> GetExportDataAsync(long formId, CancellationToken ct)
    {
        return exportRepository.GetExportRowsAsync(formId, ct);
    }

    private static async Task WriteCsvHeaderAsync(CsvWriter csv, IEnumerable<string> columns)
    {
        foreach (var key in columns)
        {
            csv.WriteField(key);
        }
        await csv.NextRecordAsync();
    }

    private async Task ExportToCsvAsync(IAsyncEnumerable<SubmissionExportRow> exportRows, Stream outputStream, CancellationToken ct)
    {
        // Define entity columns
        var entityColumns = new List<string>
        {
            nameof(SubmissionExportRow.FormId),
            nameof(SubmissionExportRow.Id),
            nameof(SubmissionExportRow.IsComplete),
            nameof(SubmissionExportRow.CreatedAt),
            nameof(SubmissionExportRow.ModifiedAt),
            nameof(SubmissionExportRow.CompletedAt)
        };

        // Get the first row to determine dynamic columns
        await using var enumerator = exportRows.GetAsyncEnumerator(ct);
        SubmissionExportRow? firstRow = null;
        if (await enumerator.MoveNextAsync())
        {
            firstRow = enumerator.Current;
        }

        List<string> dynamicColumns = new();
        if (firstRow != null && !string.IsNullOrWhiteSpace(firstRow.AnswersModel))
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(firstRow.AnswersModel);
            if (dict is not null)
            {
                dynamicColumns = dict.Keys.ToList();
            }
        }

        var orderedKeys = entityColumns.Concat(dynamicColumns).ToList();

        var writer = new StreamWriter(outputStream, leaveOpen: true);
        var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = false // We'll write the header manually
        });

        await WriteCsvHeaderAsync(csv, orderedKeys);

        // If there are no rows, just flush and return
        if (firstRow is null)
        {
            await writer.FlushAsync();
            return;
        }

        // Build an async enumerable of records for CSV streaming
        async IAsyncEnumerable<IDictionary<string, object>> GetRecords()
        {
            // Yield the first row, then the rest from the enumerator
            yield return BuildRecord(firstRow, dynamicColumns);
            while (await enumerator.MoveNextAsync())
            {
                yield return BuildRecord(enumerator.Current, dynamicColumns);
            }
        }

        await csv.WriteRecordsAsync(GetRecords(), ct);
        await writer.FlushAsync();
    }

    /// <summary>
    /// Builds a dictionary representing a CSV record from a submission row, including entity and dynamic columns.
    /// Uses JsonDocument for efficient dynamic field extraction.
    /// </summary>
    private IDictionary<string, object> BuildRecord(
        SubmissionExportRow row,
        List<string> dynamicColumns)
    {
        var record = new Dictionary<string, object>
        {
            // Entity fields
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