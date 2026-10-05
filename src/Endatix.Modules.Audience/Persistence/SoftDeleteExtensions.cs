using Endatix.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Persistence;

/// <summary>
/// Soft-deletes every row a query matches in one statement, without loading the rows.
/// </summary>
internal static class SoftDeleteExtensions
{
    public static Task<int> SoftDeleteAllAsync<TEntity>(
        this IQueryable<TEntity> rows,
        CancellationToken cancellationToken)
        where TEntity : BaseEntity
    {
        DateTime now = DateTime.UtcNow;
        return rows.ExecuteUpdateAsync(
            setters => setters
                .SetProperty(row => row.IsDeleted, true)
                .SetProperty(row => row.DeletedAt, now)
                .SetProperty(row => row.ModifiedAt, now),
            cancellationToken);
    }
}
