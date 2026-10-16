using Endatix.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Soft-deletes every row a query matches in one statement, without loading the rows.
/// </summary>
public static class SoftDeleteExtensions
{
    /// <summary>
    /// Sets <see cref="BaseEntity.IsDeleted"/>, <see cref="BaseEntity.DeletedAt"/> and <see cref="BaseEntity.ModifiedAt"/>
    /// on every row <paramref name="rows"/> matches. The statement bypasses the change tracker, so it stamps those
    /// columns itself and raises no domain event.
    /// </summary>
    /// <returns>The number of rows it deleted.</returns>
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
