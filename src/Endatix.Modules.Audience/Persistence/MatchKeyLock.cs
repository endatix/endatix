using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Endatix.Modules.Audience.Persistence;

/// <summary>
/// Keeps a match-key change apart from member inserts for one tenant. Member writers share the
/// lock, so creates on different forms run in parallel and the unique indexes settle their races.
/// A match-key change takes it exclusively. The lock lives for the transaction.
/// </summary>
internal static class MatchKeyLock
{
    private const int LockClass = 1116;

    /// <summary>For writers that add members. Many can hold it at once.</summary>
    public static Task<IDbContextTransaction> BeginSharedAsync(
        IAudienceDbContext db,
        long tenantId,
        CancellationToken cancellationToken) =>
        BeginAsync(new LockRequest(db, tenantId, Exclusive: false), cancellationToken);

    /// <summary>For a match-key change. Waits for every member writer of the tenant.</summary>
    public static Task<IDbContextTransaction> BeginExclusiveAsync(
        IAudienceDbContext db,
        long tenantId,
        CancellationToken cancellationToken) =>
        BeginAsync(new LockRequest(db, tenantId, Exclusive: true), cancellationToken);

    private static async Task<IDbContextTransaction> BeginAsync(
        LockRequest request,
        CancellationToken cancellationToken)
    {
        DbContext context = (DbContext)request.Db;
        IDbContextTransaction transaction =
            await context.Database.BeginTransactionAsync(cancellationToken);
        string tenantKey = request.TenantId.ToString();
        FormattableString sql = request.Exclusive
            ? (FormattableString)$"SELECT pg_advisory_xact_lock({LockClass}, hashtext({tenantKey}))"
            : $"SELECT pg_advisory_xact_lock_shared({LockClass}, hashtext({tenantKey}))";
        await context.Database.ExecuteSqlInterpolatedAsync(sql, cancellationToken);
        return transaction;
    }

    private sealed record LockRequest(IAudienceDbContext Db, long TenantId, bool Exclusive);
}
