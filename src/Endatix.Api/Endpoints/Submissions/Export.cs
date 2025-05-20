using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Endatix.Framework.Tooling;
using Endatix.Core.UseCases.Submissions.Export;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Microsoft.Extensions.Logging;
using MediatR;

namespace Endatix.Api.Endpoints.Submissions;

public class Export : Endpoint<ExportRequest>
{
    private readonly IMediator _mediator;
    private readonly IExporterFactory _exporterFactory;
    private readonly ILogger<Export> _logger;

    public Export(
        IMediator mediator,
        IExporterFactory exporterFactory,
        ILogger<Export> logger)
    {
        _mediator = mediator;
        _exporterFactory = exporterFactory;
        _logger = logger;
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
           s.Responses[500] = "An error occurred during export";
       });
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(ExportRequest request, CancellationToken cancellationToken)
    {
        using var measureExecution = new MeasureExecution();

        try
        {
            // Create export options (can be customized based on user preferences in the future)
            var options = new ExportOptions
            {
                // For now we use default column selection
                Columns = null,
                Transformers = null,
                Metadata = new Dictionary<string, object> { ["FormId"] = request.FormId }
            };

            // Resolve the exporter once
            var exporter = _exporterFactory.GetExporter<SubmissionExportRow>("csv");

            // Get headers first to set them before streaming
            var headers = await exporter.GetHeadersAsync(options, cancellationToken);

            // Set headers BEFORE writing anything to the response
            HttpContext.Response.ContentType = headers.ContentType;
            HttpContext.Response.Headers.ContentDisposition = $"attachment; filename={headers.FileName}";

            // Create query with the already resolved exporter
            var exportQuery = new SubmissionsExportQuery(
                FormId: request.FormId,
                Exporter: exporter,
                Options: options,
                OutputStream: HttpContext.Response.Body
            );

            // Stream directly to the response
            var result = await _mediator.Send(exportQuery, cancellationToken);

            if (!result.IsSuccess)
            {
                // This can only happen if an error occurred after headers were sent
                _logger.LogError("Export failed: {ErrorMessage}", result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in export endpoint");

            if (!HttpContext.Response.HasStarted)
            {
                HttpContext.Response.Clear();
                HttpContext.Response.StatusCode = 500;
                HttpContext.Response.ContentType = "text/plain";
                await HttpContext.Response.WriteAsync("An unexpected error occurred during export.");
            }
        }
    }
}