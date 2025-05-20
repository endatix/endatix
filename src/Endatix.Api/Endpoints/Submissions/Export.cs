using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Endatix.Framework.Tooling;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Abstractions.Exporting;
using Microsoft.Extensions.Logging;
using Endatix.Core.Entities;

namespace Endatix.Api.Endpoints.Submissions;

public class Export : Endpoint<Export.Request>
{
    private readonly ISubmissionExportRepository _exportRepository;
    private readonly IExporterFactory _exporterFactory;
    private readonly ILogger<Export> _logger;
    
    public Export(
        ISubmissionExportRepository exportRepository, 
        IExporterFactory exporterFactory, 
        ILogger<Export> logger)
    {
        _exportRepository = exportRepository;
        _exporterFactory = exporterFactory;
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
            var exporter = _exporterFactory.GetExporter<SubmissionExportRow>("csv");
            
            // Create export options (can be customized based on user preferences in the future)
            var options = new ExportOptions
            {
                // For now we use default column selection
                Columns = null,
                Transformers = null
            };

            // Set response headers before streaming starts
            HttpContext.Response.ContentType = "text/csv";
            
            // Set a default filename that includes form ID
            var defaultFileName = $"submissions-{req.FormId}.csv";
            HttpContext.Response.Headers.ContentDisposition = $"attachment; filename={defaultFileName}";
            
            // Let the exporter stream directly to the response
            await exporter.StreamExportAsync(
                records: exportRows,
                options: options,
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