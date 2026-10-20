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

    /// <summary>
    /// Soft-deletes the rows <paramref name="rows"/> matches as <see cref="SoftDeleteAllAsync{TEntity}"/> does, at
    /// most <paramref name="batchSize"/> per statement, until none is left. <paramref name="rows"/> must leave out
    /// deleted rows, as the soft-delete query filter does, or the batches would never end.
    /// </summary>
    /// <returns>The number of rows it deleted.</returns>
    public static async Task<int> SoftDeleteInBatchesAsync<TEntity>(
        this IQueryable<TEntity> rows,
        int batchSize,
        CancellationToken cancellationToken)
        where TEntity : BaseEntity
    {
        var total = 0;
        int deleted;
        do
        {
            deleted = await rows.OrderBy(row => row.Id).Take(batchSize).SoftDeleteAllAsync(cancellationToken);
            total += deleted;
        }
        while (deleted == batchSize);

        return total;
    }
}
