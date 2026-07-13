using Endatix.Core.Abstractions.Exporting;
using Endatix.Modules.Reporting.Contracts.Export;

namespace Endatix.Modules.Reporting.Features.Export;

/// <summary>
/// Resolves reporting export format definitions for the export API.
/// </summary>
internal sealed class ExportFormatDefinitionResolver(IExportFormatRepository exportFormatRepository)
    : IExportFormatDefinitionResolver
{
    public async Task<ExportFormatDefinition?> GetByIdAsync(
        long tenantId,
        long exportFormatId,
        CancellationToken cancellationToken)
    {
        var exportFormat = await exportFormatRepository.GetByIdAsync(
            tenantId,
            exportFormatId,
            cancellationToken);

        return exportFormat is null
            ? null
            : new ExportFormatDefinition(exportFormat.Id, exportFormat.Format, exportFormat.SettingsJson);
    }
}
