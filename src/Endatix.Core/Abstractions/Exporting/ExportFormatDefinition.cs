namespace Endatix.Core.Abstractions.Exporting;

/// <summary>
/// Resolved export format definition for API export configuration.
/// </summary>
public sealed record ExportFormatDefinition(
    long Id,
    string Format,
    string? SettingsJson);

/// <summary>
/// Resolves reporting export format definitions for the export API.
/// </summary>
public interface IExportFormatDefinitionResolver
{
    /// <summary>
    /// Gets an export format definition by ID.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant.</param>
    /// <param name="exportFormatId">The ID of the export format.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The export format definition, or null if not found.</returns>
    Task<ExportFormatDefinition?> GetByIdAsync(
        long tenantId,
        long exportFormatId,
        CancellationToken cancellationToken);
}
