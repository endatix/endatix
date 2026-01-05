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
using Endatix.Core.Entities;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Represents a validated export operation with resolved format, item type, and SQL function name.
/// </summary>
internal sealed record ValidatedExportOperation(
    string Format,
    Type ItemType,
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
                exporter = _exporterFactory.GetExporter(validatedExportOperation.Format, validatedExportOperation.ItemType);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Unsupported export format requested: {Format} for type {ItemType}",
                    validatedExportOperation.Format, validatedExportOperation.ItemType.Name);
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
                await SetErrorResponse(string.Join(", ", result.Errors), StatusCodes.Status500InternalServerError);
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

            var itemType = ResolveExportItemType(exportConfig.ItemTypeName);
            if (itemType is null)
            {
                return Result.Invalid(new ValidationError(
                    $"Export configuration {request.ExportId.Value} has invalid ItemTypeName: {exportConfig.ItemTypeName}"));
            }

            if (!typeof(IExportItem).IsAssignableFrom(itemType))
            {
                _logger.LogWarning("Export configuration {RequestExportId} specifies type {ItemTypeName} which does not implement IExportItem",
                    request.ExportId.Value, exportConfig.ItemTypeName);
                return Result.Invalid(new ValidationError($"Invalid item type: {exportConfig.ItemTypeName}"));
            }

            var exportIdBasedOperation = new ValidatedExportOperation(
                exportConfig.Format,
                itemType,
                exportConfig.SqlFunctionName);
            return Result.Success(exportIdBasedOperation);
        }

        var format = string.IsNullOrWhiteSpace(request.ExportFormat) ? DEFAULT_EXPORT_FORMAT : request.ExportFormat;
        var defaultItemType = typeof(Endatix.Core.Entities.SubmissionExportRow);
        var formatBasedExportOperation = new ValidatedExportOperation(format, defaultItemType, null);
        return Result.Success(formatBasedExportOperation);
    }

    /// <summary>
    /// Resolves the Type from a fully qualified type name string.
    /// Optimized to search only in relevant assemblies (Endatix.Core) first,
    /// then falls back to all assemblies if not found.
    /// </summary>
    private static Type? ResolveExportItemType(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return typeof(SubmissionExportRow);
        }

        // Try to resolve from all loaded assemblies (Type.GetType searches mscorlib and calling assembly)
        var type = Type.GetType(typeName, false);
        if (type is not null && typeof(IExportItem).IsAssignableFrom(type))
        {
            return type;
        }

        var endatixCoreAssembly = typeof(IExportItem).Assembly;
        type = endatixCoreAssembly.GetType(typeName, false);
        if (type is not null && typeof(IExportItem).IsAssignableFrom(type))
        {
            return type;
        }

        return null;
    }

    /// <summary>
    /// Sets the error response for the current HTTP context.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="statusCode">The status code to set.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
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