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
using Endatix.Core.Abstractions.Authorization;
using TenantSettingsEntity = Endatix.Core.Entities.TenantSettings;
using Endatix.Core.Entities;
using System.IO.Pipelines;
using Endatix.Modules.Reporting.Contracts.Export;

namespace Endatix.Api.Endpoints.Submissions;

/// <summary>
/// Represents a validated export operation with resolved format, item type, SQL function name, and optional page size.
/// </summary>
internal sealed record ValidatedExportOperation(
    string Format,
    Type ItemType,
    string? SqlFunctionName,
    int? ExportPageSize = null,
    SubmissionExportExecutionSettings? ExecutionSettings = null
);

/// <summary>
/// Endpoint for exporting form submissions.
/// </summary>
public partial class Export : Endpoint<ExportRequest>
{
    private const string ERROR_MESSAGE_COULD_NOT_DETERMINE_EXPORT_FORMAT = "Could not determine export format. Either provide ExportFormatId or a valid ExportId.";
    private readonly IMediator _mediator;
    private readonly IExporterFactory _exporterFactory;
    private readonly IFormsRepository _formsRepository;
    private readonly IRepository<TenantSettingsEntity> _tenantSettingsRepository;
    private readonly IExportFormatRepository? _exportFormatRepository;
    private readonly IExportCapabilityRegistry? _exportCapabilityRegistry;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<Export> _logger;

    public Export(
        IMediator mediator,
        IExporterFactory exporterFactory,
        IFormsRepository formsRepository,
        IRepository<TenantSettingsEntity> tenantSettingsRepository,
        ITenantContext tenantContext,
        ILogger<Export> logger,
        IExportFormatRepository? exportFormatRepository = null,
        IExportCapabilityRegistry? exportCapabilityRegistry = null)
    {
        _mediator = mediator;
        _exporterFactory = exporterFactory;
        _formsRepository = formsRepository;
        _tenantSettingsRepository = tenantSettingsRepository;
        _exportFormatRepository = exportFormatRepository;
        _exportCapabilityRegistry = exportCapabilityRegistry;
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
           s.Responses[400] = "Invalid export request or unsupported format";
           s.Responses[404] = "Form not found. Cannot export submissions";
           s.Responses[409] = "Export preconditions not met (e.g. form schema not compiled, reporting read model empty)";
           s.Responses[500] = "An unexpected error occurred during export";
       });
    }

    /// <inheritdoc/>
    public override async Task HandleAsync(ExportRequest request, CancellationToken cancellationToken)
    {
        PipeWriter? pipeWriter = null;

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
                Formatters = null,
                Metadata = new Dictionary<string, object> { ["FormId"] = request.FormId }
            };

            if (validatedExportOperation.ExecutionSettings is not null)
            {
                options.Metadata[SubmissionExportMetadataKeys.ExecutionSettings] =
                    validatedExportOperation.ExecutionSettings;
            }

            var headersResult = await exporter.GetHeadersAsync(options, cancellationToken);
            if (!headersResult.IsSuccess)
            {
                _logger.LogError("Failed to get export headers: {Errors}", string.Join(", ", headersResult.Errors));
                await SetErrorResponse("Failed to get export output options", StatusCodes.Status500InternalServerError);
                return;
            }

            var fileExport = headersResult.Value;

            HttpContext.Response.ContentType = fileExport.ContentType;
            HttpContext.Response.Headers.ContentDisposition = $"attachment; filename={fileExport.FileName}";

            pipeWriter = HttpContext.Response.BodyWriter;
            var exportQuery = new SubmissionsExportQuery(
                FormId: request.FormId,
                TenantId: _tenantContext.TenantId,
                Exporter: exporter,
                Options: options,
                OutputWriter: pipeWriter,
                SqlFunctionName: validatedExportOperation.SqlFunctionName,
                ExportPageSize: validatedExportOperation.ExportPageSize
            );

            var result = await _mediator.Send(exportQuery, cancellationToken);

            if (!result.IsSuccess)
            {
                var errors = string.Join(", ", result.Errors);
                LogExportFailedError(request.FormId, errors);

                await SetErrorResponse(
                    errors,
                    MapExportFailureStatusCode(result.Status),
                    new InvalidOperationException(errors),
                    pipeWriter);
                return;
            }

            await pipeWriter.FlushAsync(cancellationToken);
            await pipeWriter.CompleteAsync();
            _logger.LogDebug("Successfully exported submissions for form {FormId}", request.FormId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled error in export endpoint");
            await SetErrorResponse("An unexpected error occurred during export.", StatusCodes.Status500InternalServerError, ex, pipeWriter);
        }
    }

    private async Task<Result<ValidatedExportOperation>> ResolveExportConfigurationAsync(
        ExportRequest request,
        CancellationToken cancellationToken)
    {
        var form = await _formsRepository.GetByIdAsync(request.FormId, cancellationToken);
        if (form is null)
        {
            return Result.Invalid(new ValidationError($"Form with ID {request.FormId} not found"));
        }

        if (request.ExportFormatId.HasValue)
        {
            return await ResolveExportFormatIdConfigurationAsync(request, cancellationToken);
        }

        if (_exportFormatRepository is not null)
        {
            var defaultFormatResult =
                await ResolveTenantDefaultExportFormatAsync(request, cancellationToken);
            if (defaultFormatResult is not null)
            {
                return defaultFormatResult;
            }
        }

        if (request.ExportId.HasValue)
        {
            return await ResolveExportIdConfigurationAsync(request, cancellationToken);
        }

        if (_exportCapabilityRegistry is not null)
        {
            return Result.Invalid(new ValidationError(
                "ExportFormatId is required when reporting export formats are enabled."));
        }

        return Result.Invalid(new ValidationError(ERROR_MESSAGE_COULD_NOT_DETERMINE_EXPORT_FORMAT));
    }

    private async Task<Result<ValidatedExportOperation>> ResolveExportFormatIdConfigurationAsync(
        ExportRequest request,
        CancellationToken cancellationToken)
    {
        if (_exportFormatRepository is null || _exportCapabilityRegistry is null)
        {
            return Result.Invalid(new ValidationError("Reporting export formats are not available."));
        }

        var exportFormatId = request.ExportFormatId!.Value;
        var exportFormat = await _exportFormatRepository.GetByIdAsync(
            _tenantContext.TenantId,
            exportFormatId,
            cancellationToken);
        if (exportFormat is null)
        {
            _logger.LogWarning(
                "Export format {ExportFormatId} not found for tenant {TenantId}",
                exportFormatId,
                _tenantContext.TenantId);
            return Result.Invalid(new ValidationError($"Export format with ID {exportFormatId} not found"));
        }

        return BuildValidatedExportOperation(request, exportFormat);
    }

    private async Task<Result<ValidatedExportOperation>?> ResolveTenantDefaultExportFormatAsync(
        ExportRequest request,
        CancellationToken cancellationToken)
    {
        var exportFormat = await _exportFormatRepository!.GetTenantDefaultAsync(
            _tenantContext.TenantId,
            cancellationToken);

        return exportFormat is null
            ? null
            : BuildValidatedExportOperation(request, exportFormat);
    }

    private Result<ValidatedExportOperation> BuildValidatedExportOperation(
        ExportRequest request,
        ExportFormatRecord exportFormat)
    {
        if (_exportCapabilityRegistry is null ||
            !_exportCapabilityRegistry.TryGetByWireKey(exportFormat.WireKey, out var capability))
        {
            return Result.Invalid(new ValidationError(
                $"Export format with ID {exportFormat.Id} uses an unsupported capability."));
        }

        var itemType = ResolveExportItemType(capability.ItemTypeName);
        if (itemType is null)
        {
            return Result.Invalid(new ValidationError(
                $"Export format with ID {exportFormat.Id} has an invalid item type."));
        }

        var disallowedFilters = ExportRequestFilterGuard.GetDisallowedWireNames(
            capability.AllowedFilters,
            CreateExportFilterContext(request));
        if (disallowedFilters.Count > 0)
        {
            return Result.Invalid(new ValidationError(
                $"Filter(s) not supported for export capability '{capability.WireKey}': {string.Join(", ", disallowedFilters)}."));
        }

        SubmissionExportExecutionSettings executionSettings = new(
            ExportFormatId: exportFormat.Id,
            SettingsJson: exportFormat.SettingsJson,
            IncludeTestSubmissions: request.IncludeTestSubmissions,
            ColumnScope: NormalizeColumnScope(request.ColumnScope),
            Locale: NormalizeLocale(request.Locale),
            CreatedAfter: request.CreatedAfter,
            CreatedBefore: request.CreatedBefore,
            StartedAfter: request.StartedAfter,
            StartedBefore: request.StartedBefore,
            CompletedAfter: request.CompletedAfter,
            CompletedBefore: request.CompletedBefore,
            MinSubmissionId: request.MinSubmissionId,
            MaxSubmissionId: request.MaxSubmissionId,
            IsComplete: MapCompletionStatusToIsComplete(request.CompletionStatus));

        return Result.Success(new ValidatedExportOperation(
            exportFormat.WireKey,
            itemType,
            null,
            null,
            executionSettings));
    }

    private async Task<Result<ValidatedExportOperation>> ResolveExportIdConfigurationAsync(
        ExportRequest request,
        CancellationToken cancellationToken)
    {
        var disallowedOnLegacy = ExportRequestFilterGuard.GetDisallowedWireNames(
            ExportRequestFilters.None,
            CreateExportFilterContext(request));
        if (disallowedOnLegacy.Count > 0)
        {
            return Result.Invalid(new ValidationError(
                $"Filter(s) are not supported on legacy ExportId exports: {string.Join(", ", disallowedOnLegacy)}. Use ExportFormatId."));
        }

        var exportId = request.ExportId!.Value;
        var spec = new TenantSettingsByTenantIdSpec(_tenantContext.TenantId);
        var tenantSettings = await _tenantSettingsRepository.FirstOrDefaultAsync(spec, cancellationToken);
        if (tenantSettings is null)
        {
            _logger.LogWarning("No tenant settings found for tenant {TenantId}", _tenantContext.TenantId);
            return Result.Invalid(new ValidationError("No tenant settings found"));
        }

        var exportConfig = tenantSettings.CustomExports.FirstOrDefault(e => e.Id == exportId);
        if (exportConfig is null)
        {
            _logger.LogWarning("Export with ID {ExportId} not found for tenant {TenantId}", exportId, _tenantContext.TenantId);
            return Result.Invalid(new ValidationError($"Export with ID {exportId} not found"));
        }

        if (string.IsNullOrWhiteSpace(exportConfig.Format))
        {
            return Result.Invalid(new ValidationError($"Export configuration {exportId} has no format specified"));
        }

        var itemType = ResolveExportItemType(exportConfig.ItemTypeName);
        if (itemType is null)
        {
            return Result.Invalid(new ValidationError(
                $"Export configuration {exportId} has invalid ItemTypeName: {exportConfig.ItemTypeName}"));
        }

        if (!typeof(IExportItem).IsAssignableFrom(itemType))
        {
            _logger.LogWarning(
                "Export configuration {RequestExportId} specifies type {ItemTypeName} which does not implement IExportItem",
                exportId,
                exportConfig.ItemTypeName);
            return Result.Invalid(new ValidationError($"Invalid item type: {exportConfig.ItemTypeName}"));
        }

        return Result.Success(new ValidatedExportOperation(
            exportConfig.Format,
            itemType,
            exportConfig.SqlFunctionName,
            exportConfig.ExportPageSize));
    }

    private static ExportFilterContext CreateExportFilterContext(ExportRequest request) =>
        new(
            IncludeTestSubmissions: request.IncludeTestSubmissions,
            CreatedAfter: request.CreatedAfter,
            CreatedBefore: request.CreatedBefore,
            StartedAfter: request.StartedAfter,
            StartedBefore: request.StartedBefore,
            CompletedAfter: request.CompletedAfter,
            CompletedBefore: request.CompletedBefore,
            MinSubmissionId: request.MinSubmissionId,
            MaxSubmissionId: request.MaxSubmissionId,
            Locale: request.Locale,
            ColumnScope: NormalizeColumnScope(request.ColumnScope),
            CompletionStatus: request.CompletionStatus);

    private static string[]? NormalizeColumnScope(string[]? columnScope) =>
        columnScope is { Length: > 0 } ? columnScope : null;

    private static string? NormalizeLocale(string? locale) =>
        string.IsNullOrWhiteSpace(locale) ? null : locale.Trim();

    private static bool? MapCompletionStatusToIsComplete(ExportCompletionStatus? completionStatus) =>
        completionStatus switch
        {
            ExportCompletionStatus.Completed => true,
            ExportCompletionStatus.Incomplete => false,
            _ => null,
        };

    private static Type? ResolveExportItemType(string? typeName)
    {
        if (string.IsNullOrWhiteSpace(typeName))
        {
            return typeof(SubmissionExportRow);
        }

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

    private static int MapExportFailureStatusCode(ResultStatus status) =>
        status switch
        {
            ResultStatus.Invalid => StatusCodes.Status400BadRequest,
            ResultStatus.NotFound => StatusCodes.Status404NotFound,
            ResultStatus.Conflict => StatusCodes.Status409Conflict,
            ResultStatus.Forbidden => StatusCodes.Status403Forbidden,
            ResultStatus.Unauthorized => StatusCodes.Status401Unauthorized,
            _ => StatusCodes.Status500InternalServerError,
        };

    private async Task SetErrorResponse(string message, int? statusCode = null, Exception? exception = null, PipeWriter? pipeWriter = null)
    {
        if (HttpContext.Response.HasStarted)
        {
            await CompletePipeIfNeeded(pipeWriter, exception, message);
            return;
        }

        try
        {
            HttpContext.Response.Clear();
        }
        catch (InvalidOperationException)
        {
            await CompletePipeIfNeeded(pipeWriter, exception, message);
            return;
        }

        int resolvedStatus = statusCode ?? StatusCodes.Status500InternalServerError;
        HttpContext.Response.StatusCode = resolvedStatus;
        HttpContext.Response.ContentType = "application/problem+json";

        // Emit RFC7807 camelCase so Hub (and other clients) can surface Detail in the UI.
        var problem = new Microsoft.AspNetCore.Mvc.ProblemDetails
        {
            Title = "Export failed",
            Detail = message,
            Status = resolvedStatus,
            Type = resolvedStatus switch
            {
                StatusCodes.Status400BadRequest => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                StatusCodes.Status404NotFound => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
                StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc7231#section-6.5.8",
                _ => "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            },
        };

        await HttpContext.Response.WriteAsJsonAsync(problem);
    }

    private async Task CompletePipeIfNeeded(PipeWriter? pipeWriter, Exception? exception, string fallbackMessage)
    {
        if (pipeWriter is null)
        {
            return;
        }

        try
        {
            await pipeWriter.CompleteAsync(exception ?? new InvalidOperationException(fallbackMessage));
        }
        catch (Exception ex)
        {
            LogCompletePipeException(ex);
        }
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to complete pipe with exception after export error")]
    private partial void LogCompletePipeException(Exception ex);


    [LoggerMessage(Level = LogLevel.Error, Message = "Export failed. FormId: {FormId}, Errors: {Errors}")]
    private partial void LogExportFailedError(long formId, string errors);
}
