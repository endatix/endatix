using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Endatix.Modules.Personalization.Persistence;

/// <summary>
/// Serializes match-key changes with member inserts for one tenant.
/// The lock lives for the transaction and releases on commit or rollback.
/// </summary>
internal static class MatchKeyLock
{
    private const int LockClass = 1116;

    public static async Task<IDbContextTransaction> BeginAsync(
        IPersonalizationDbContext db,
        long tenantId,
        CancellationToken cancellationToken)
    {
        DbContext context = (DbContext)db;
        IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT pg_advisory_xact_lock({LockClass}, hashtext({tenantId.ToString()}))",
            cancellationToken);
        return transaction;
    }
}
