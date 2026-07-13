namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Reads tenant-scoped export format definitions from the reporting schema.
/// </summary>
public interface IExportFormatRepository
{
    Task<ExportFormatRecord?> GetByIdAsync(long tenantId, long exportFormatId, CancellationToken cancellationToken);
}

/// <summary>
/// Lightweight export format row for export resolution.
/// </summary>
public sealed record ExportFormatRecord(
    long Id,
    string Name,
    string Format,
    string? SettingsJson);
