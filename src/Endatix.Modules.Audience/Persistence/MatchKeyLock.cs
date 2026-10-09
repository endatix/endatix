using System.Globalization;
using Endatix.Infrastructure.Data.Locking;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Endatix.Modules.Audience.Persistence;

/// <summary>
/// Keeps a match-key change apart from member inserts for one tenant. Member writers share the
/// lock, so creates on different forms run in parallel and the unique indexes settle their races.
/// A match-key change takes it exclusively. The lock lives for the transaction.
/// </summary>
internal sealed class MatchKeyLock(ITransactionLock transactionLock)
{
    /// <summary>For writers that add members. Many can hold it at once.</summary>
    public Task<IDbContextTransaction> BeginSharedAsync(
        IAudienceDbContext db,
        long tenantId,
        CancellationToken cancellationToken) =>
        BeginAsync(db, Request(tenantId) with { Mode = TransactionLockMode.Shared }, cancellationToken);

    /// <summary>For a match-key change. Waits for every member writer of the tenant.</summary>
    public Task<IDbContextTransaction> BeginExclusiveAsync(
        IAudienceDbContext db,
        long tenantId,
        CancellationToken cancellationToken) =>
        BeginAsync(db, Request(tenantId), cancellationToken);

    private static TransactionLockRequest Request(long tenantId) =>
        new(TransactionLockScopes.AudienceMatchKey, tenantId.ToString(CultureInfo.InvariantCulture));

    private async Task<IDbContextTransaction> BeginAsync(
        IAudienceDbContext db,
        TransactionLockRequest request,
        CancellationToken cancellationToken)
    {
        DatabaseFacade database = ((DbContext)db).Database;
        IDbContextTransaction transaction = await database.BeginTransactionAsync(cancellationToken);
        await transactionLock.AcquireAsync(database, request, cancellationToken);
        return transaction;
    }
}
