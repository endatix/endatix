using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Endatix.Infrastructure.Data.Locking;

/// <summary>
/// A named lock held until the transaction that took it ends, for work that must not interleave across processes:
/// a waiter blocks until the holder commits or rolls back, then reads what it left.
/// </summary>
/// <remarks>
/// <para>One implementation per database provider, registered by that provider's persistence builder.</para>
/// <para>
/// Take at most one lock per transaction, as its first statement. Two transactions that each hold a lock, or rows,
/// and wait for the other's can deadlock; one that takes a single lock before it touches anything cannot.
/// </para>
/// <para>
/// Run the whole locked section on the connection that holds the lock: another context joins it with
/// <c>SetDbConnection</c> + <c>UseTransactionAsync</c>. Renting a second pooled connection while holding the lock can
/// exhaust the pool under load. Pattern and checklist: ARCHITECTURE.md, Serializing work across requests.
/// </para>
/// <para>
/// When the lock is not taken, for whatever reason, the caller rolls the transaction back (disposing it does).
/// PostgreSQL has already aborted it; on SQL Server it stays open, still holding whatever it locked before.
/// </para>
/// </remarks>
public interface ITransactionLock
{
    /// <summary>
    /// Waits until no other transaction holds the lock in a conflicting mode, then holds it until the current
    /// transaction of <paramref name="database"/> ends.
    /// </summary>
    /// <exception cref="TransactionLockTimeoutException">The lock was not free within the request's timeout.</exception>
    /// <exception cref="InvalidOperationException">
    /// No transaction is open, so the lock would end with the statement; or SQL Server gave the wait up to break a
    /// deadlock, cancelled it itself, or refused the lock. PostgreSQL reports those as its own exception.
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// <paramref name="cancellationToken"/> was cancelled during the wait. A provider error raised for another reason
    /// is thrown as it is, even when the token was cancelled as well.
    /// </exception>
    /// <remarks>
    /// A request without a timeout waits for as long as the command may run. When the command timeout ends the wait
    /// first, the provider's own exception is thrown as it is (a <c>SqlException</c> or an <c>NpgsqlException</c>),
    /// not <see cref="TransactionLockTimeoutException"/>.
    /// </remarks>
    Task AcquireAsync(DatabaseFacade database, TransactionLockRequest request, CancellationToken cancellationToken);
}
