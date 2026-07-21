using System.Text.Json;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Reporting.Data;

/// <summary>
/// Repository for tenant export format definitions.
/// </summary>
internal sealed class ExportFormatRepository(
    ReportingDbContext dbContext,
    IReportingUnitOfWork unitOfWork,
    ExportFormatSettingsParser settingsParser,
    IExportCapabilityRegistry capabilityRegistry) : IExportFormatRepository
{
    private static readonly string _defaultSubmissionsSettingsJson = JsonSerializer.Serialize(new
    {
        aliasProfile = "native",
        keySeparator = "__",
        includeTestSubmissions = false,
    });

    private static readonly string _defaultCodebookSettingsJson = JsonSerializer.Serialize(new
    {
        aliasProfile = "native",
        keySeparator = "__",
    });

    /// <inheritdoc />
    public async Task<ExportFormatRecord?> GetByIdAsync(
        long tenantId,
        long exportFormatId,
        CancellationToken cancellationToken)
    {
        var exportFormat = await dbContext.ExportFormats
            .AsNoTracking()
            .Where(format => format.TenantId == tenantId && format.Id == exportFormatId)
            .FirstOrDefaultAsync(cancellationToken);

        return exportFormat is null ? null : MapRecord(exportFormat);
    }

    /// <inheritdoc />
    public async Task<ExportFormatRecord?> GetTenantDefaultAsync(
        long tenantId,
        CancellationToken cancellationToken)
    {
        var defaultMapping = await dbContext.SurveyTypeExportMappings
            .AsNoTracking()
            .Include(mapping => mapping.ExportFormat)
            .Where(mapping =>
                mapping.TenantId == tenantId &&
                mapping.IsDefault &&
                mapping.SurveyTypeId == null)
            .FirstOrDefaultAsync(cancellationToken);

        return defaultMapping?.ExportFormat is null ? null : MapRecord(defaultMapping.ExportFormat);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ExportFormatDto>> ListAsync(
        long tenantId,
        CancellationToken cancellationToken)
    {
        var exportFormats = await dbContext.ExportFormats
            .AsNoTracking()
            .Where(format => format.TenantId == tenantId)
            .OrderBy(format => format.Name)
            .ToListAsync(cancellationToken);

        return exportFormats.Select(MapDto).ToList();
    }

    /// <inheritdoc />
    public async Task<ExportFormatDto?> GetAdminByIdAsync(
        long tenantId,
        long exportFormatId,
        CancellationToken cancellationToken)
    {
        var exportFormat = await dbContext.ExportFormats
            .AsNoTracking()
            .Where(format => format.TenantId == tenantId && format.Id == exportFormatId)
            .FirstOrDefaultAsync(cancellationToken);

        return exportFormat is null ? null : MapDto(exportFormat);
    }

    /// <inheritdoc />
    public async Task<ExportFormatDto> CreateAsync(
        long tenantId,
        string name,
        ExportTarget exportTarget,
        ExportDeliveryFormat deliveryFormat,
        ExportProfile profile,
        string? description,
        string? settingsJson,
        CancellationToken cancellationToken)
    {
        ExportFormat exportFormat = new(
            tenantId,
            name.Trim(),
            exportTarget,
            deliveryFormat,
            profile,
            description?.Trim());
        exportFormat.UpdateSettingsJson(settingsJson);

        await dbContext.ExportFormats.AddAsync(exportFormat, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return MapDto(exportFormat);
    }

    /// <inheritdoc />
    public async Task<ExportFormatDto?> UpdateAsync(
        long tenantId,
        long exportFormatId,
        string? name,
        string? description,
        string? settingsJson,
        CancellationToken cancellationToken)
    {
        var exportFormat = await dbContext.ExportFormats
            .Where(format => format.TenantId == tenantId && format.Id == exportFormatId)
            .FirstOrDefaultAsync(cancellationToken);

        if (exportFormat is null)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(name))
        {
            exportFormat.UpdateName(name.Trim());
        }

        if (description is not null)
        {
            exportFormat.UpdateDescription(string.IsNullOrWhiteSpace(description) ? null : description.Trim());
        }

        if (settingsJson is not null)
        {
            exportFormat.UpdateSettingsJson(settingsJson);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return MapDto(exportFormat);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(long tenantId, long exportFormatId, CancellationToken cancellationToken)
    {
        var exportFormat = await dbContext.ExportFormats
            .Where(format => format.TenantId == tenantId && format.Id == exportFormatId)
            .FirstOrDefaultAsync(cancellationToken);

        if (exportFormat is null)
        {
            return false;
        }

        exportFormat.Delete();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public async Task<bool> IsReferencedByMappingAsync(
        long tenantId,
        long exportFormatId,
        CancellationToken cancellationToken) =>
        await dbContext.SurveyTypeExportMappings
            .AsNoTracking()
            .AnyAsync(
                mapping => mapping.TenantId == tenantId && mapping.ExportFormatId == exportFormatId,
                cancellationToken);

    /// <inheritdoc />
    public async Task SeedDefaultsAsync(long tenantId, CancellationToken cancellationToken)
    {
        var hasFormats = await dbContext.ExportFormats
            .AsNoTracking()
            .AnyAsync(format => format.TenantId == tenantId, cancellationToken);

        if (hasFormats)
        {
            return;
        }

        ExportFormat csvFormat = new(
            tenantId,
            "CSV",
            ExportTarget.Submissions,
            ExportDeliveryFormat.Csv,
            ExportProfile.Native,
            "Default CSV export for form submissions");
        csvFormat.UpdateSettingsJson(_defaultSubmissionsSettingsJson);

        ExportFormat jsonFormat = new(
            tenantId,
            "JSON",
            ExportTarget.Submissions,
            ExportDeliveryFormat.Json,
            ExportProfile.Native,
            "Default JSON export for form submissions");
        jsonFormat.UpdateSettingsJson(_defaultSubmissionsSettingsJson);

        ExportFormat codebookFormat = new(
            tenantId,
            "Codebook",
            ExportTarget.Codebook,
            ExportDeliveryFormat.Json,
            ExportProfile.Native,
            "Default form definition codebook export");
        codebookFormat.UpdateSettingsJson(_defaultCodebookSettingsJson);

        await dbContext.ExportFormats.AddRangeAsync([csvFormat, jsonFormat, codebookFormat], cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var hasDefaultMapping = await dbContext.SurveyTypeExportMappings
            .AsNoTracking()
            .AnyAsync(
                mapping =>
                    mapping.TenantId == tenantId &&
                    mapping.IsDefault &&
                    mapping.SurveyTypeId == null,
                cancellationToken);

        if (!hasDefaultMapping)
        {
            SurveyTypeExportMapping defaultMapping = new(
                tenantId,
                csvFormat.Id,
                surveyTypeId: null,
                isDefault: true);

            await dbContext.SurveyTypeExportMappings.AddAsync(defaultMapping, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private ExportFormatDto MapDto(ExportFormat exportFormat)
    {
        var capability = ResolveCapability(exportFormat);

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
            exportFormat.ModifiedAt,
            AllowedExportFilters.ToAllowedFilterNames(capability.AllowedFilters));
    }

    private ExportFormatRecord MapRecord(ExportFormat exportFormat)
    {
        var capability = ResolveCapability(exportFormat);

        return new ExportFormatRecord(
            exportFormat.Id,
            exportFormat.Name,
            exportFormat.ExportTarget,
            exportFormat.DeliveryFormat,
            exportFormat.Profile,
            capability.WireKey,
            exportFormat.SettingsJson);
    }

    private ExportCapability ResolveCapability(ExportFormat exportFormat)
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

        return capability;
    }
}
