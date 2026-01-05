using FastEndpoints;
using Microsoft.AspNetCore.Http;
using Endatix.Core.UseCases.Submissions.Export;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Abstractions;
using Endatix.Core.Specifications;
using Endatix.Core.Infrastructure.Domain;
using Microsoft.Extensions.Logging;
using MediatR;
using Endatix.Core.Infrastructure.Result;
using System.Text.Json;
using Endatix.Core.Abstractions.Authorization;
using TenantSettingsEntity = Endatix.Core.Entities.TenantSettings;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Represents a validated export operation with resolved format and SQL function name.
/// </summary>
internal sealed record ValidatedExportOperation(
    string Format,
    string? SqlFunctionName
);

/// <summary>
/// Endpoint for exporting form submissions.
/// </summary>
public class Export : Endpoint<ExportRequest>
{
    private const string DEFAULT_EXPORT_FORMAT = "csv";
    private const string ERROR_MESSAGE_COULD_NOT_DETERMINE_EXPORT_FORMAT = "Could not determine export format. Either provide ExportFormat or a valid ExportId.";
    private readonly IMediator _mediator;
    private readonly IExporterFactory _exporterFactory;
    private readonly IFormsRepository _formsRepository;
    private readonly IRepository<TenantSettingsEntity> _tenantSettingsRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<Export> _logger;

    public Export(
        IMediator mediator,
        IExporterFactory exporterFactory,
        IFormsRepository formsRepository,
        IRepository<TenantSettingsEntity> tenantSettingsRepository,
        ITenantContext tenantContext,
        ILogger<Export> logger)
    {
        _mediator = mediator;
        _exporterFactory = exporterFactory;
        _formsRepository = formsRepository;
        _tenantSettingsRepository = tenantSettingsRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("forms/{formId}/submissions/export");
        Permissions(Actions.Submissions.Export);
        Summary(s =>
       {
           s.Summary = "Export submissions";
           s.Description = "Export submissions for a given form";
           s.Responses[200] = "The submissions were successfully exported";
           s.Responses[404] = "Form not found. Cannot export submissions";
           s.Responses[500] = "An error occurred during export";
       });
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(ExportRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var exportValidationResult = await ResolveExportConfigurationAsync(request, cancellationToken);
            if (!exportValidationResult.IsSuccess)
            {
                var validationError = exportValidationResult.ValidationErrors.FirstOrDefault();
                await SetErrorResponse(validationError?.ErrorMessage ?? ERROR_MESSAGE_COULD_NOT_DETERMINE_EXPORT_FORMAT, StatusCodes.Status400BadRequest);
                return;
            }

            var validatedExportOperation = exportValidationResult.Value;
            IExporter exporter;
            try
            {
                exporter = _exporterFactory.GetExporter(validatedExportOperation.Format);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Unsupported export format requested: {Format}", validatedExportOperation.Format);
                await SetErrorResponse(ex.Message, StatusCodes.Status400BadRequest);
                return;
            }

            var options = new ExportOptions
            {
                Columns = null,
                Transformers = null,
                Metadata = new Dictionary<string, object> { ["FormId"] = request.FormId }
            };

            var headersResult = await exporter.GetHeadersAsync(options, cancellationToken);
            if (!headersResult.IsSuccess)
            {
                _logger.LogError("Failed to get export headers: {Errors}", string.Join(", ", headersResult.Errors));
                await SetErrorResponse("Failed to get export output options", StatusCodes.Status500InternalServerError);
                return;
            }

            var fileExport = headersResult.Value;

            // Set response headers before streaming
            // Note: We intentionally don't set Content-Length here as this is a streaming response.
            // The response will automatically use chunked transfer encoding which is well-supported
            // by modern HTTP clients and allows for true streaming without buffering the entire output.
            HttpContext.Response.ContentType = fileExport.ContentType;
            HttpContext.Response.Headers.ContentDisposition = $"attachment; filename={fileExport.FileName}";

            // Execute the export
            var pipeWriter = HttpContext.Response.BodyWriter;
            var exportQuery = new SubmissionsExportQuery(
                FormId: request.FormId,
                Exporter: exporter,
                Options: options,
                OutputWriter: pipeWriter,
                SqlFunctionName: validatedExportOperation.SqlFunctionName
            );

            var result = await _mediator.Send(exportQuery, cancellationToken);

            if (!result.IsSuccess)
            {
                await HandleErrorResult(result);
                return;
            }

            await pipeWriter.FlushAsync(cancellationToken);
            await pipeWriter.CompleteAsync();
            _logger.LogDebug("Successfully exported submissions for form {FormId}", request.FormId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in export endpoint");
            await SetErrorResponse("An unexpected error occurred during export.");
        }
    }

    /// <summary>
    /// Resolves and validates the export configuration for the given request.
    /// </summary>
    /// <param name="request">The export request.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The validated export operation.</returns>
    private async Task<Result<ValidatedExportOperation>> ResolveExportConfigurationAsync(
        ExportRequest request,
        CancellationToken cancellationToken)
    {
        var form = await _formsRepository.GetByIdAsync(request.FormId, cancellationToken);
        if (form is null)
        {
            return Result.Invalid(new ValidationError($"Form with ID {request.FormId} not found"));
        }

        if (request.ExportId.HasValue)
        {
            var spec = new TenantSettingsByTenantIdSpec(_tenantContext.TenantId);
            var tenantSettings = await _tenantSettingsRepository.FirstOrDefaultAsync(spec, cancellationToken);
            if (tenantSettings is null)
            {
                _logger.LogWarning("No tenant settings found for tenant {TenantId}", _tenantContext.TenantId);
                return Result.Invalid(new ValidationError("No tenant settings found"));
            }

            var customExports = tenantSettings.CustomExports;
            var exportConfig = customExports.FirstOrDefault(e => e.Id == request.ExportId.Value);
            if (exportConfig is null)
            {
                _logger.LogWarning("Export with ID {ExportId} not found for tenant {TenantId}", request.ExportId.Value, _tenantContext.TenantId);
                return Result.Invalid(new ValidationError($"Export with ID {request.ExportId.Value} not found"));
            }

            if (string.IsNullOrWhiteSpace(exportConfig.Format))
            {
                return Result.Invalid(new ValidationError($"Export configuration {request.ExportId.Value} has no format specified"));
            }

            var exportIdBasedOperation = new ValidatedExportOperation(exportConfig.Format, exportConfig.SqlFunctionName);
            return Result.Success(exportIdBasedOperation);
        }

        var format = string.IsNullOrWhiteSpace(request.ExportFormat) ? DEFAULT_EXPORT_FORMAT : request.ExportFormat;
        var formatBasedExportOperation = new ValidatedExportOperation(format, null);
        return Result.Success(formatBasedExportOperation);
    }

    private async Task<bool> HandleErrorResult<T>(Result<T> result)
    {
        if (result.Status == ResultStatus.NotFound)
        {
            await SetErrorResponse("Form not found", StatusCodes.Status404NotFound);
            return true;
        }

        _logger.LogError("Export failed due to: {Errors}", string.Join(", ", result.Errors));
        await SetErrorResponse(string.Join(", ", result.Errors));
        return true;
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