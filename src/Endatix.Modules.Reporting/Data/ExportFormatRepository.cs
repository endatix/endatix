using Endatix.Modules.Reporting.Contracts.Export;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Reporting.Data;

/// <summary>
/// Repository for tenant export format definitions.
/// </summary>
internal sealed class ExportFormatRepository(ReportingDbContext dbContext) : IExportFormatRepository
{

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

        return exportFormat is null
            ? null
            : Map(exportFormat);
    }

    private static ExportFormatRecord Map(ExportFormat exportFormat) =>
        new(
            exportFormat.Id,
            exportFormat.Name,
            MapSerializationType(exportFormat.SerializationType),
            exportFormat.SettingsJson);

    private static string MapSerializationType(ExportSerializationType serializationType) =>
        serializationType switch
        {
            ExportSerializationType.Csv => "csv",
            ExportSerializationType.Json => "json",
            ExportSerializationType.Codebook => "codebook",
            _ => throw new ArgumentOutOfRangeException(nameof(serializationType), serializationType, "Unsupported export serialization type."),
        };
}
