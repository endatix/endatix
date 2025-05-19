using System.Globalization;
using System.Text.Json;
using CsvHelper;
using CsvHelper.Configuration;
using FastEndpoints;
using Microsoft.AspNetCore.Mvc;
using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

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
        var exportRows = await GetExportDataAsync(req.FormId, ct);
        var csvStream = new MemoryStream();
        await ExportToCsvAsync(exportRows, csvStream, ct);
        csvStream.Position = 0;

        await SendStreamAsync(
            csvStream,
            fileName: $"submissions-{req.FormId}.csv",
            contentType: "text/csv",
            cancellation: ct);
    }

    private async Task<List<SubmissionExportRow>> GetExportDataAsync(long formId, CancellationToken ct)
    {
        return await dbContext.Set<SubmissionExportRow>()
            .FromSqlRaw("SELECT * FROM export_form_submissions({0})", formId)
            .ToListAsync(ct);
    }

    private async Task ExportToCsvAsync(List<SubmissionExportRow> exportRows, Stream outputStream, CancellationToken ct)
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

        // Determine dynamic columns from the first row
        List<string> dynamicColumns = new();
        if (exportRows.Count > 0 && !string.IsNullOrWhiteSpace(exportRows[0].AnswersModel))
        {
            var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(exportRows[0].AnswersModel);
            if (dict is not null)
            {
                dynamicColumns = dict.Keys.ToList();
            }
        }

        var orderedKeys = entityColumns.Concat(dynamicColumns).ToList();

        using var writer = new StreamWriter(outputStream, leaveOpen: true);
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        });

        // Write header
        foreach (var key in orderedKeys)
        {
            csv.WriteField(key);
        }
        csv.NextRecord();

        // Stream each row directly
        foreach (var row in exportRows)
        {
            // Entity fields
            csv.WriteField(row.FormId.ToString());
            csv.WriteField(row.Id.ToString());
            csv.WriteField(row.IsComplete.ToString());
            csv.WriteField(row.CreatedAt.ToString("o"));
            csv.WriteField(row.ModifiedAt?.ToString("o") ?? string.Empty);
            csv.WriteField(row.CompletedAt?.ToString("o") ?? string.Empty);

            // Dynamic fields
            Dictionary<string, JsonElement>? dict = null;
            if (!string.IsNullOrWhiteSpace(row.AnswersModel))
            {
                dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(row.AnswersModel);
            }

            foreach (var key in dynamicColumns)
            {
                string value = string.Empty;
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
                csv.WriteField(value);
            }
            csv.NextRecord();
        }

        await writer.FlushAsync();
    }
}