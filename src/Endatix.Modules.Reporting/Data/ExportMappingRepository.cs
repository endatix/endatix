using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Reporting.Data;

/// <summary>
/// Repository for tenant export format mappings.
/// </summary>
internal sealed class ExportMappingRepository(
    ReportingDbContext dbContext,
    IReportingUnitOfWork unitOfWork,
    ExportFormatSettingsParser settingsParser,
    IExportCapabilityRegistry capabilityRegistry) : IExportMappingRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<ExportMappingDto>> ListAsync(
        long tenantId,
        CancellationToken cancellationToken)
    {
        var mappings = await dbContext.SurveyTypeExportMappings
            .AsNoTracking()
            .Include(mapping => mapping.ExportFormat)
            .Where(mapping => mapping.TenantId == tenantId)
            .OrderBy(mapping => mapping.SurveyTypeId)
            .ThenBy(mapping => mapping.ExportFormat.Name)
            .ToListAsync(cancellationToken);

        return mappings.Select(MapDto).ToList();
    }

    /// <inheritdoc />
    public async Task<ExportMappingDto?> UpsertAsync(
        long tenantId,
        UpsertExportMappingRequest request,
        CancellationToken cancellationToken)
    {
        var exportFormat = await dbContext.ExportFormats
            .AsNoTracking()
            .Where(format => format.TenantId == tenantId && format.Id == request.ExportFormatId)
            .FirstOrDefaultAsync(cancellationToken);

        if (exportFormat is null)
        {
            return null;
        }

        if (request.IsDefault)
        {
            var scopedDefaults = await dbContext.SurveyTypeExportMappings
                .Where(mapping =>
                    mapping.TenantId == tenantId &&
                    mapping.SurveyTypeId == request.SurveyTypeId &&
                    mapping.IsDefault)
                .ToListAsync(cancellationToken);

            foreach (var mapping in scopedDefaults)
            {
                mapping.ClearDefault();
            }
        }

        var existingMapping = await dbContext.SurveyTypeExportMappings
            .FirstOrDefaultAsync(
                mapping =>
                    mapping.TenantId == tenantId &&
                    mapping.SurveyTypeId == request.SurveyTypeId &&
                    mapping.ExportFormatId == request.ExportFormatId,
                cancellationToken);

        if (existingMapping is null)
        {
            existingMapping = new SurveyTypeExportMapping(
                tenantId,
                request.ExportFormatId,
                request.SurveyTypeId,
                request.IsDefault);

            await dbContext.SurveyTypeExportMappings.AddAsync(existingMapping, cancellationToken);
        }
        else if (request.IsDefault)
        {
            existingMapping.MarkAsDefault();
        }
        else
        {
            existingMapping.ClearDefault();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var savedMapping = await dbContext.SurveyTypeExportMappings
            .AsNoTracking()
            .Include(mapping => mapping.ExportFormat)
            .FirstOrDefaultAsync(mapping => mapping.Id == existingMapping.Id, cancellationToken);

        return savedMapping is null ? null : MapDto(savedMapping);
    }

    private ExportFormatDto MapExportFormat(ExportFormat exportFormat)
    {
        if (!capabilityRegistry.TryGet(
                exportFormat.ExportTarget,
                exportFormat.DeliveryFormat,
                exportFormat.Profile,
                out var capability))
        {
            throw new InvalidOperationException(
                $"Unsupported export format configuration: target={exportFormat.ExportTarget}, delivery={exportFormat.DeliveryFormat}, profile={exportFormat.Profile}.");
        }

        return new ExportFormatDto(
            exportFormat.Id,
            exportFormat.Name,
            exportFormat.ExportTarget,
            exportFormat.DeliveryFormat,
            exportFormat.Profile,
            capability.WireKey,
            capability.Label,
            exportFormat.Description,
            settingsParser.Parse(exportFormat.SettingsJson),
            exportFormat.CreatedAt,
            exportFormat.ModifiedAt);
    }

    private ExportMappingDto MapDto(SurveyTypeExportMapping mapping) =>
        new(
            mapping.Id,
            mapping.ExportFormatId,
            mapping.SurveyTypeId,
            mapping.IsDefault,
            MapExportFormat(mapping.ExportFormat));
}
