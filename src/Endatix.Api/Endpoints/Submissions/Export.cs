using System.Globalization;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using FastEndpoints;
using Microsoft.AspNetCore.Mvc;
using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Endatix.Framework.Tooling;

namespace Endatix.Api.Endpoints.Submissions;

public class Export(AppDbContext dbContext) : Endpoint<Export.Request>
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
        using var dbg = new MeasureExecution();

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
        return dbContext.Set<SubmissionExportRow>()
            .FromSqlRaw("SELECT * FROM export_form_submissions({0})", formId)
            .AsNoTracking()
            .AsAsyncEnumerable();
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
        SubmissionExportRow? firstRow = null;
        await foreach (var row in exportRows.WithCancellation(ct))
        {
            firstRow = row;
            break;
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

        // Write header
        foreach (var key in orderedKeys)
        {
            csv.WriteField(key);
        }
        csv.NextRecord();

        // Build an async enumerable of dictionaries for all rows
        async IAsyncEnumerable<IDictionary<string, object>> GetRecords()
        {
            if (firstRow != null)
            {
                yield return BuildRecord(firstRow, entityColumns, dynamicColumns);
            }

            var skipFirst = true;
            await foreach (var row in exportRows.WithCancellation(ct))
            {
                if (skipFirst) { skipFirst = false; continue; }
                yield return BuildRecord(row, entityColumns, dynamicColumns);
            }
        }

        await csv.WriteRecordsAsync(GetRecords(), ct);
        await writer.FlushAsync();
    }

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

        // Dynamic fields
        Dictionary<string, JsonElement>? dict = null;
        if (!string.IsNullOrWhiteSpace(row.AnswersModel))
        {
            dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(row.AnswersModel);
        }

        foreach (var key in dynamicColumns)
        {
            var value = string.Empty;
            if (dict is not null && dict.TryGetValue(key, out var element))
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

        return record;
    }
}