using Endatix.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Soft-deletes the rows a query matches without loading them: in one statement, or in batches by id.
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
    /// Soft-deletes the live rows <paramref name="rows"/> matches as <see cref="SoftDeleteAllAsync{TEntity}"/> does,
    /// at most <paramref name="batchSize"/> per statement. Each batch is the next ids after the last one deleted, so
    /// no statement reads again the rows earlier batches deleted, and the deleted rows are left out here, so the
    /// batches end even where a caller drops the soft-delete query filter.
    /// </summary>
    /// <remarks>
    /// The plan is the database's to choose, and neither keeps a batch to its own rows for certain. SQL Server can seek
    /// <c>IX_Submissions_FormId</c>, which carries the clustered id, but may prefer the filtered
    /// <c>IX_Submissions_FormId_CollectionStatus</c>, on which the id narrows nothing: each batch then reads and sorts
    /// all the form's rows still live. That is a known gap, left to a follow-up. PostgreSQL has no index on a form's
    /// rows in id order, so it scans the primary key from the last id and skips other forms' rows: linear in the span
    /// of the form's ids rather than in its size, which an index would fix at the cost of a migration that builds one
    /// on Submissions.
    /// </remarks>
    /// <returns>The number of rows it deleted.</returns>
    public static Task<int> SoftDeleteInBatchesAsync<TEntity>(
        this IQueryable<TEntity> rows,
        int batchSize,
        CancellationToken cancellationToken)
        where TEntity : BaseEntity =>
        new IdBatches<TEntity>(rows.Where(row => !row.IsDeleted), batchSize).SoftDeleteEveryBatchAsync(cancellationToken);

    /// <summary>A batch's highest id, and how many live rows it holds.</summary>
    private sealed record IdBatch(long Last, int Count);

    /// <summary>The live rows of a query, a batch at a time, in id order.</summary>
    private sealed class IdBatches<TEntity>(IQueryable<TEntity> live, int batchSize)
        where TEntity : BaseEntity
    {
        public async Task<int> SoftDeleteEveryBatchAsync(CancellationToken cancellationToken)
        {
            var total = 0;
            var after = long.MinValue;
            while (await NextAsync(after, cancellationToken) is { } batch)
            {
                total += await SoftDeleteAsync(after, batch.Last, cancellationToken);
                if (batch.Count < batchSize)
                {
                    break;
                }

                after = batch.Last;
            }

            return total;
        }

        /// <summary>The next batch after <paramref name="after"/>, or <see langword="null"/> when none is left.</summary>
        private async Task<IdBatch?> NextAsync(long after, CancellationToken cancellationToken)
        {
            var ids = await live
                .Where(row => row.Id > after)
                .OrderBy(row => row.Id)
                .Select(row => row.Id)
                .Take(batchSize)
                .ToListAsync(cancellationToken);
            return ids.Count == 0 ? null : new IdBatch(ids[^1], ids.Count);
        }

        /// <summary>Soft-deletes the live rows with ids over <paramref name="after"/> up to <paramref name="last"/>.</summary>
        private Task<int> SoftDeleteAsync(long after, long last, CancellationToken cancellationToken) =>
            live.Where(row => row.Id > after && row.Id <= last).SoftDeleteAllAsync(cancellationToken);
    }
}
