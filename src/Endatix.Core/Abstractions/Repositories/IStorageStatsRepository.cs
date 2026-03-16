using Endatix.Core.UseCases.Stats.Models;

namespace Endatix.Core.Abstractions.Repositories;

/// <summary>
/// Repository for storage statistics.
/// </summary>
public interface IStorageStatsRepository
{
    /// <summary>
    /// Get the storage statistics for a tenant.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant to get the statistics for. If null, the statistics for all tenants will be returned.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The storage statistics for the tenant.</returns>
    Task<TenantStorageStats> GetTenantStats(long? tenantId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the storage statistics for a form.
    /// </summary>
    /// <param name="tenantId">The ID of the tenant to get the statistics for. If null, the statistics for all tenants will be returned.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The storage statistics for the form.</returns>
    Task<IReadOnlyList<FormStorageStats>> GetFormStats(long? tenantId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the storage statistics for a table.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The storage statistics for the table.</returns>
    Task<IReadOnlyList<TableStorageStats>> GetTableStats(CancellationToken cancellationToken = default);
}
