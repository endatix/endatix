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
    private const string CsvFormatName = "CSV";

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
    /// <remarks>
    /// Every read here uses <c>IgnoreQueryFilters</c> with explicit <c>TenantId</c> and
    /// <c>IsDeleted</c> predicates, the same shape <c>FlattenedSubmissionRepository</c> uses. This
    /// method runs at tenant provisioning, where the ambient tenant is not the tenant being seeded;
    /// under the tenant filter each guard would read "TenantId == ambient AND TenantId == target",
    /// never match, and re-seed a tenant that already has its defaults.
    /// </remarks>
    public async Task SeedDefaultsAsync(long tenantId, CancellationToken cancellationToken)
    {
        await unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var seeded = await SeedDefaultsCoreAsync(tenantId, cancellationToken);
            await unitOfWork.CommitTransactionAsync(cancellationToken);

            if (!seeded)
            {
                return;
            }
        }
        catch (DbUpdateException)
        {
            // Two provisioning flows can reach the guards before either writes. The loser hits the
            // filtered unique index on (TenantId, Name) or on the tenant default mapping. Both wanted
            // the same end state, so if the winner produced it this is success, not a 500.
            await unitOfWork.RollbackTransactionAsync(cancellationToken);
            dbContext.ChangeTracker.Clear();

            if (await IsFullySeededAsync(tenantId, cancellationToken))
            {
                return;
            }

            throw;
        }
    }

    /// <summary>
    /// Creates whatever part of the tenant default set is missing. Returns false when nothing was
    /// written, so the caller can skip work on the common already-seeded path.
    /// </summary>
    private async Task<bool> SeedDefaultsCoreAsync(long tenantId, CancellationToken cancellationToken)
    {
        var liveFormats = await dbContext.ExportFormats
            .IgnoreQueryFilters()
            .Where(format => format.TenantId == tenantId && !format.IsDeleted)
            .ToListAsync(cancellationToken);

        var wrote = false;

        if (liveFormats.Count == 0)
        {
            liveFormats = BuildDefaultFormats(tenantId);
            await dbContext.ExportFormats.AddRangeAsync(liveFormats, cancellationToken);
            wrote = true;
        }

        // The mapping must point at a format that still exists. GetTenantDefaultAsync resolves it via
        // Include, which the soft-delete filter nulls out, so a mapping left pointing at a deleted
        // format reports "no default" forever. Repoint it instead of skipping on mapping-exists.
        var targetFormat =
            liveFormats.FirstOrDefault(format => format.Name == CsvFormatName) ?? liveFormats[0];

        var defaultMapping = await dbContext.SurveyTypeExportMappings
            .IgnoreQueryFilters()
            .Where(mapping =>
                mapping.TenantId == tenantId &&
                mapping.IsDefault &&
                mapping.SurveyTypeId == null &&
                !mapping.IsDeleted)
            .FirstOrDefaultAsync(cancellationToken);

        if (defaultMapping is null)
        {
            SurveyTypeExportMapping mapping = new(
                tenantId,
                targetFormat.Id,
                surveyTypeId: null,
                isDefault: true);

            await dbContext.SurveyTypeExportMappings.AddAsync(mapping, cancellationToken);
            wrote = true;
        }
        else if (!liveFormats.Exists(format => format.Id == defaultMapping.ExportFormatId))
        {
            defaultMapping.UpdateExportFormat(targetFormat.Id);
            wrote = true;
        }

        if (wrote)
        {
            // One SaveChanges for formats and mapping together: the previous two-phase write could
            // commit the formats and then fail, leaving a tenant whose formats exist — so the guard
            // returns early — but whose default mapping never gets created on any retry.
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return wrote;
    }

    private async Task<bool> IsFullySeededAsync(long tenantId, CancellationToken cancellationToken)
    {
        var hasFormats = await dbContext.ExportFormats
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(format => format.TenantId == tenantId && !format.IsDeleted, cancellationToken);

        if (!hasFormats)
        {
            return false;
        }

        return await dbContext.SurveyTypeExportMappings
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(
                mapping =>
                    mapping.TenantId == tenantId &&
                    mapping.IsDefault &&
                    mapping.SurveyTypeId == null &&
                    !mapping.IsDeleted,
                cancellationToken);
    }

    private List<ExportFormat> BuildDefaultFormats(long tenantId)
    {
        ExportFormat csvFormat = new(
            tenantId,
            CsvFormatName,
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

        return [csvFormat, jsonFormat, codebookFormat];
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
