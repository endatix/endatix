using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Endatix.Framework.Tooling;
using Endatix.Core.UseCases.Submissions.Export;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Entities;
using Microsoft.Extensions.Logging;
using MediatR;
using Endatix.Core.Infrastructure.Result;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

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
            // Create export options (to be customized based on user preferences in the future)
            var options = new ExportOptions
            {
                Columns = null,
                Transformers = null,
                Metadata = new Dictionary<string, object> { ["FormId"] = request.FormId }
            };

            var exporter = _exporterFactory.GetExporter<SubmissionExportRow>("csv");
            var headersResult = await exporter.GetHeadersAsync(options, cancellationToken);

            if (!headersResult.IsSuccess)
            {
                _logger.LogError("Failed to get export headers: {Errors}", string.Join(", ", headersResult.Errors));
                await SetErrorResponse("Failed to get export headers");
                return;
            }

            var fileExport = headersResult.Value;

            // Set response headers BEFORE streaming
            HttpContext.Response.ContentType = fileExport.ContentType;
            HttpContext.Response.Headers.ContentDisposition = $"attachment; filename={fileExport.FileName}";

            // Create export query
            var exportQuery = new SubmissionsExportQuery(
                FormId: request.FormId,
                Exporter: exporter,
                Options: options,
                OutputStream: HttpContext.Response.Body
            );

            // Execute the export
            var result = await _mediator.Send(exportQuery, cancellationToken);

            if (!result.IsSuccess)
            {
                if (result.Status == ResultStatus.NotFound)
                {
                    await SetErrorResponse("Form not found", StatusCodes.Status400BadRequest);
                    return;
                }

                _logger.LogError("Export failed due to: {Errors}", string.Join(", ", result.Errors));
                await SetErrorResponse(string.Join(", ", result.Errors));
                return;

            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in export endpoint");
            await SetErrorResponse("An unexpected error occurred during export.");
        }
    }

    private async Task SetErrorResponse(string message, int? statusCode = null)
    {
        if (!HttpContext.Response.HasStarted)
        {
            HttpContext.Response.Clear();
        }

        HttpContext.Response.StatusCode = statusCode ?? StatusCodes.Status500InternalServerError;
        HttpContext.Response.ContentType = "application/json";

        var problem = new FastEndpoints.ProblemDetails
        {
            Detail = message,
            Status = statusCode ?? StatusCodes.Status500InternalServerError,
        };

        await HttpContext.Response.WriteAsync(JsonSerializer.Serialize(problem));
    }
}