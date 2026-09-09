using System.Text.Json;
using Endatix.Core.Abstractions.Data;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Features.Export;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Endatix.Modules.Reporting.Data;

/// <summary>
/// Repository for tenant export format definitions.
/// </summary>
internal sealed class ExportFormatRepository(
    ReportingDbContext dbContext,
    IReportingUnitOfWork unitOfWork,
    ExportFormatSettingsParser settingsParser,
    IExportCapabilityRegistry capabilityRegistry,
    IUniqueConstraintViolationChecker uniqueViolationChecker) : IExportFormatRepository
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

    private sealed record DefaultFormat(
        string Name,
        ExportTarget Target,
        ExportDeliveryFormat Delivery,
        string Description);

    /// <summary>Every tenant default is <see cref="ExportProfile.Native"/>; settings follow the target.</summary>
    private static readonly DefaultFormat[] DefaultFormats =
    [
        new("CSV", ExportTarget.Submissions, ExportDeliveryFormat.Csv, "Default CSV export for form submissions"),
        new("JSON", ExportTarget.Submissions, ExportDeliveryFormat.Json, "Default JSON export for form submissions"),
        new("Excel (XLSX)", ExportTarget.Submissions, ExportDeliveryFormat.Xlsx, "Default Excel export for form submissions"),
        new("Codebook", ExportTarget.Codebook, ExportDeliveryFormat.Json, "Default form definition codebook export"),
    ];

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
        var existing = await FormatsForTenant(tenantId)
            .AsNoTracking()
            .Where(format => format.Profile == ExportProfile.Native)
            .Select(format => new { format.ExportTarget, format.DeliveryFormat })
            .ToListAsync(cancellationToken);

        var missing = DefaultFormats.Where(definition => !existing.Any(format =>
            format.ExportTarget == definition.Target &&
            format.DeliveryFormat == definition.Delivery));

        // One save per row: BaseEntity.Id is DatabaseGeneratedOption.None; two unsaved rows collide on Id = 0.
        foreach (var definition in missing)
        {
            var entry = dbContext.ExportFormats.Add(CreateDefault(tenantId, definition));
            await SaveOrConcedeAsync(entry, cancellationToken);
        }

        await EnsureDefaultMappingAsync(tenantId, cancellationToken);
    }

    private static ExportFormat CreateDefault(long tenantId, DefaultFormat definition)
    {
        ExportFormat format = new(
            tenantId,
            definition.Name,
            definition.Target,
            definition.Delivery,
            ExportProfile.Native,
            definition.Description);

        format.UpdateSettingsJson(definition.Target == ExportTarget.Codebook
            ? _defaultCodebookSettingsJson
            : _defaultSubmissionsSettingsJson);

        return format;
    }

    /// <summary>
    /// Tenant default is Native CSV. Repoint when the mapped format was soft-deleted
    /// (<see cref="GetTenantDefaultAsync"/> uses a filtered <c>Include</c>).
    /// </summary>
    private async Task EnsureDefaultMappingAsync(long tenantId, CancellationToken cancellationToken)
    {
        var csvFormatId = await FormatsForTenant(tenantId)
            .AsNoTracking()
            .Where(format =>
                format.ExportTarget == ExportTarget.Submissions &&
                format.DeliveryFormat == ExportDeliveryFormat.Csv &&
                format.Profile == ExportProfile.Native)
            .Select(format => (long?)format.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (csvFormatId is null)
        {
            return;
        }

        var defaultMapping = await MappingsForTenant(tenantId)
            .FirstOrDefaultAsync(
                mapping => mapping.IsDefault && mapping.SurveyTypeId == null,
                cancellationToken);

        if (defaultMapping is null)
        {
            var entry = dbContext.SurveyTypeExportMappings.Add(
                new(tenantId, csvFormatId.Value, surveyTypeId: null, isDefault: true));
            await SaveOrConcedeAsync(entry, cancellationToken);
            defaultMapping = await MappingsForTenant(tenantId)
                .FirstOrDefaultAsync(
                    mapping => mapping.IsDefault && mapping.SurveyTypeId == null,
                    cancellationToken);
            if (defaultMapping is null)
            {
                return;
            }
        }

        var pointsAtLiveFormat = await FormatsForTenant(tenantId)
            .AsNoTracking()
            .AnyAsync(format => format.Id == defaultMapping.ExportFormatId, cancellationToken);

        if (pointsAtLiveFormat)
        {
            return;
        }

        defaultMapping.PointTo(csvFormatId.Value);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Concurrent seed of the same tenant races on unique indexes. Concede only when the live row
    /// is the default we meant to insert — a different profile reusing the name is not a race.
    /// </summary>
    private async Task SaveOrConcedeAsync(EntityEntry entry, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
            when (uniqueViolationChecker.AnalyzeUniqueConstraint(exception).IsUniqueConstraintViolation)
        {
            entry.State = EntityState.Detached;
            if (!await IsSameDefaultAsync(entry.Entity, cancellationToken))
            {
                throw;
            }
        }
    }

    private async Task<bool> IsSameDefaultAsync(object entity, CancellationToken cancellationToken)
    {
        switch (entity)
        {
            case ExportFormat format:
                var existing = await FormatsForTenant(format.TenantId)
                    .AsNoTracking()
                    .FirstOrDefaultAsync(row => row.Name == format.Name, cancellationToken);
                return existing is not null
                    && existing.ExportTarget == format.ExportTarget
                    && existing.DeliveryFormat == format.DeliveryFormat
                    && existing.Profile == format.Profile;
            case SurveyTypeExportMapping mapping:
                return await MappingsForTenant(mapping.TenantId)
                    .AsNoTracking()
                    .AnyAsync(
                        row => row.IsDefault && row.SurveyTypeId == mapping.SurveyTypeId,
                        cancellationToken);
            default:
                return false;
        }
    }

    /// <summary>
    /// Outbox <c>tenant.created</c> is app-level; drop only the tenant filter, keep soft-delete.
    /// </summary>
    private IQueryable<ExportFormat> FormatsForTenant(long tenantId) =>
        dbContext.ExportFormats
            .IgnoreQueryFilters([EndatixQueryFilterNames.Tenant])
            .Where(format => format.TenantId == tenantId);

    /// <inheritdoc cref="FormatsForTenant" />
    private IQueryable<SurveyTypeExportMapping> MappingsForTenant(long tenantId) =>
        dbContext.SurveyTypeExportMappings
            .IgnoreQueryFilters([EndatixQueryFilterNames.Tenant])
            .Where(mapping => mapping.TenantId == tenantId);

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
