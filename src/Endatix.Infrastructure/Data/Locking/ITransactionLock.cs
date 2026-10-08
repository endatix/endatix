using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Endatix.Infrastructure.Data.Locking;

/// <summary>
/// A named lock held until the transaction that took it ends, for work that must not interleave across processes:
/// a waiter blocks until the holder commits or rolls back, then reads what it left.
/// </summary>
/// <remarks>
/// One implementation per database provider, registered by that provider's persistence builder.
/// </remarks>
public interface ITransactionLock
{
    /// <summary>
    /// Waits until no other transaction holds the lock in a conflicting mode, then holds it until the current
    /// transaction of <paramref name="database"/> ends.
    /// </summary>
    /// <exception cref="TransactionLockTimeoutException">The lock was not free within the request's timeout.</exception>
    /// <exception cref="InvalidOperationException">No transaction is open, so the lock would end with the statement.</exception>
    /// <exception cref="NotSupportedException">The database provider has no transaction lock.</exception>
    Task AcquireAsync(DatabaseFacade database, TransactionLockRequest request, CancellationToken cancellationToken);
}
