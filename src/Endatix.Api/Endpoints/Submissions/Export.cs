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
        var allRecords = new List<Dictionary<string, string>>();
        var allKeys = new HashSet<string>
        {
            nameof(SubmissionExportRow.FormId),
            nameof(SubmissionExportRow.Id),
            nameof(SubmissionExportRow.IsComplete),
            nameof(SubmissionExportRow.CreatedAt),
            nameof(SubmissionExportRow.ModifiedAt),
            nameof(SubmissionExportRow.CompletedAt)
        };

        foreach (var row in exportRows)
        {
            var record = new Dictionary<string, string>
            {
                [nameof(SubmissionExportRow.FormId)] = row.FormId.ToString(),
                [nameof(SubmissionExportRow.Id)] = row.Id.ToString(),
                [nameof(SubmissionExportRow.IsComplete)] = row.IsComplete.ToString(),
                [nameof(SubmissionExportRow.CreatedAt)] = row.CreatedAt.ToString("o"),
                [nameof(SubmissionExportRow.ModifiedAt)] = row.ModifiedAt?.ToString("o"),
                [nameof(SubmissionExportRow.CompletedAt)] = row.CompletedAt?.ToString("o")
            };

            // Merge in AnswersModel fields if present
            if (!string.IsNullOrWhiteSpace(row.AnswersModel))
            {
                var dict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(row.AnswersModel);
                if (dict is not null)
                {
                    foreach (var kvp in dict)
                    {
                        record[kvp.Key] = kvp.Value.ValueKind switch
                        {
                            JsonValueKind.String => kvp.Value.GetString(),
                            JsonValueKind.Number => kvp.Value.ToString(),
                            JsonValueKind.True => "true",
                            JsonValueKind.False => "false",
                            JsonValueKind.Null => null,
                            _ => kvp.Value.ToString()
                        };
                        allKeys.Add(kvp.Key);
                    }
                }
            }

            allRecords.Add(record);
        }

        var orderedKeys = allKeys.ToList();

        using var writer = new StreamWriter(outputStream, leaveOpen: true);
        using var csv = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            HasHeaderRecord = true
        });

        foreach (var key in orderedKeys)
        {
            csv.WriteField(key);
        }
        csv.NextRecord();

        foreach (var record in allRecords)
        {
            foreach (var key in orderedKeys)
            {
                csv.WriteField(record[key]);
            }
            csv.NextRecord();
        }

        await writer.FlushAsync();
    }
}