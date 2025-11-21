using MediatR;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Infrastructure.Result;
using Microsoft.Extensions.Logging;
using Endatix.Core.Specifications;
using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Domain;
using TenantSettingsEntity = Endatix.Core.Entities.TenantSettings;

namespace Endatix.Core.UseCases.Submissions.Export;

public sealed class SubmissionsExportHandler : IRequestHandler<SubmissionsExportQuery, Result<FileExport>>
{
    private readonly ISubmissionExportRepository _exportRepository;
    private readonly IFormsRepository _formsRepository;
    private readonly IRepository<TenantSettingsEntity> _tenantSettingsRepository;
    private readonly ITenantContext _tenantContext;
    private readonly ILogger<SubmissionsExportHandler> _logger;

    public SubmissionsExportHandler(
        ISubmissionExportRepository exportRepository,
        IFormsRepository formsRepository,
        IRepository<TenantSettingsEntity> tenantSettingsRepository,
        ITenantContext tenantContext,
        ILogger<SubmissionsExportHandler> logger)
    {
        _exportRepository = exportRepository;
        _formsRepository = formsRepository;
        _tenantSettingsRepository = tenantSettingsRepository;
        _tenantContext = tenantContext;
        _logger = logger;
    }

    public async Task<Result<FileExport>> Handle(SubmissionsExportQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // Get options with FormId in metadata
            var options = request.GetOptionsWithFormId();
            var form = await _formsRepository.GetByIdAsync(request.FormId, cancellationToken);
            if (form is null)
            {
                return Result<FileExport>.NotFound($"Form with ID {request.FormId} not found");
            }

            var sqlFunctionName = await GetSqlFunctionName(request.ExportId, cancellationToken);

            // Get the export data stream
            var exportRows = _exportRepository.GetExportRowsAsync(request.FormId, sqlFunctionName, cancellationToken);
            
            // Stream the export directly to the provided output writer
            return await request.Exporter.StreamExportAsync(
                records: exportRows,
                options: options,
                cancellationToken: cancellationToken,
                writer: request.OutputWriter);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting submissions for form {FormId}", request.FormId);
            return Result<FileExport>.Error($"Export failed: {ex.Message}");
        }
    }

    private async Task<string?> GetSqlFunctionName(long? exportId, CancellationToken cancellationToken)
    {
        if (!exportId.HasValue)
        {
            return null;
        }

        var spec = new TenantSettingsByTenantIdSpec(_tenantContext.TenantId);
        var tenantSettings = await _tenantSettingsRepository.FirstOrDefaultAsync(spec, cancellationToken);
        if (tenantSettings is null)
        {
            _logger.LogWarning("No tenant settings found for tenant {TenantId}", _tenantContext.TenantId);
            return null;
        }

        var customExports = tenantSettings.CustomExports;
        var exportConfig = customExports.FirstOrDefault(e => e.Id == exportId.Value);
        if (exportConfig is null)
        {
            _logger.LogWarning("Export with ID {ExportId} not found for tenant {TenantId}", exportId.Value, _tenantContext.TenantId);
            return null;
        }

        return exportConfig.SqlFunctionName;
    }
}