using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Endatix.Framework.Tooling;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Abstractions.Exporting;
using Microsoft.Extensions.Logging;

namespace Endatix.Api.Endpoints.Submissions;

public class Export : Endpoint<Export.Request>
{
    private readonly ISubmissionExportRepository _exportRepository;
    private readonly IExporter _exporter;
    private readonly ILogger<Export> _logger;
    public Export(ISubmissionExportRepository exportRepository, IExporter exporter, ILogger<Export> logger)
    {
        _exportRepository = exportRepository;
        _exporter = exporter;
        _logger = logger;
    }

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
            var exportRows = _exportRepository.GetExportRowsAsync(req.FormId, ct);
            var fileName = $"submissions-{req.FormId}.csv";
            var contentType = "text/csv";

            HttpContext.Response.ContentType = contentType;
            HttpContext.Response.Headers.ContentDisposition = $"attachment; filename={fileName}";

            await _exporter.StreamExportAsync(
                exportRows,
                columns: null, // columns are determined by the exporter for now
                format: "csv",
                cancellationToken: ct,
                outputStream: HttpContext.Response.Body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting submissions");
            if (!HttpContext.Response.HasStarted)
            {
                HttpContext.Response.StatusCode = 500;
                await HttpContext.Response.WriteAsync("An error occurred during export.");
            }
        }
    }
}