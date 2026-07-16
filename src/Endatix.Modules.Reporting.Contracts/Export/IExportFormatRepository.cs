namespace Endatix.Modules.Reporting.Contracts.Export;

/// <summary>
/// Lightweight export format row for export resolution.
/// </summary>
public sealed record ExportFormatRecord(
    long Id,
    string Name,
    ExportTarget Target,
    ExportDeliveryFormat DeliveryFormat,
    ExportProfile Profile,
    string WireKey,
    string? SettingsJson);

/// <summary>
/// Reads and writes tenant-scoped export format definitions from the reporting schema.
/// </summary>
public interface IExportFormatRepository
{
    /// <summary>
    /// Gets an export format by its ID.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant.</param>
    /// <param name="exportFormatId">The ID of the export format.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The export format record, or null if not found.</returns>
    Task<ExportFormatRecord?> GetByIdAsync(long tenantId, long exportFormatId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the default export format for a tenant.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The default export format record, or null if not found.</returns>
    Task<ExportFormatRecord?> GetTenantDefaultAsync(long tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Lists all export formats for a tenant.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of export format records.</returns>
    Task<IReadOnlyList<ExportFormatDto>> ListAsync(long tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets an export format by its ID for admin purposes.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant.</param>
    /// <param name="exportFormatId">The ID of the export format.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The export format record, or null if not found.</returns>
    Task<ExportFormatDto?> GetAdminByIdAsync(long tenantId, long exportFormatId, CancellationToken cancellationToken);

    /// <summary>
    /// Creates a new export format.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant.</param>
    /// <param name="name">The name of the export format.</param>
    /// <param name="exportTarget">The target of the export format.</param>
    /// <param name="deliveryFormat">The delivery format of the export format.</param>
    /// <param name="profile">The profile of the export format.</param>
    /// <param name="description">The description of the export format.</param>
    /// <param name="settingsJson">The settings of the export format.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The created export format record.</returns>
    Task<ExportFormatDto> CreateAsync(
        long tenantId,
        string name,
        ExportTarget exportTarget,
        ExportDeliveryFormat deliveryFormat,
        ExportProfile profile,
        string? description,
        string? settingsJson,
        CancellationToken cancellationToken);

    /// <summary>
    /// Updates an export format.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant.</param>
    /// <param name="exportFormatId">The ID of the export format.</param>
    /// <param name="name">The name of the export format.</param>
    /// <param name="description">The description of the export format.</param>
    /// <param name="settingsJson">The settings of the export format.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The updated export format record, or null if not found.</returns>
    Task<ExportFormatDto?> UpdateAsync(
        long tenantId,
        long exportFormatId,
        string? name,
        string? description,
        string? settingsJson,
        CancellationToken cancellationToken);

    /// <summary>
    /// Deletes an export format.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant.</param>
    /// <param name="exportFormatId">The ID of the export format.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if the export format was deleted, false if not found.</returns>
    Task<bool> DeleteAsync(long tenantId, long exportFormatId, CancellationToken cancellationToken);

    /// <summary>
    /// Checks if an export format is referenced by a mapping.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant.</param>
    /// <param name="exportFormatId">The ID of the export format.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if the export format is referenced by a mapping, false if not.</returns>
    Task<bool> IsReferencedByMappingAsync(long tenantId, long exportFormatId, CancellationToken cancellationToken);

    /// <summary>
    /// Seeds the default export formats for a tenant.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>True if the export formats were seeded, false if not.</returns>
    Task SeedDefaultsAsync(long tenantId, CancellationToken cancellationToken);
}

/// <summary>
/// Reads and writes tenant export format mappings.
/// </summary>
public interface IExportMappingRepository
{
    /// <summary>
    /// Lists all export mappings for a tenant.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>A list of export mapping records.</returns>
    Task<IReadOnlyList<ExportMappingDto>> ListAsync(long tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Upserts an export mapping.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant.</param>
    /// <param name="request">The request to upsert the export mapping.</param>
    /// <param name="cancellationToken">A token to cancel the operation.</param>
    /// <returns>The upserted export mapping record, or null if not found.</returns>
    Task<ExportMappingDto?> UpsertAsync(
        long tenantId,
        UpsertExportMappingRequest request,
        CancellationToken cancellationToken);
}
