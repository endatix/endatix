using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;

namespace Endatix.Infrastructure.Data.Locking;

/// <summary>
/// Opens a transaction whose first statement takes an <see cref="ITransactionLock"/>.
/// </summary>
public static class TransactionLockExtensions
{
    /// <summary>
    /// Begins a transaction on <paramref name="database"/> and takes <paramref name="request"/> in it. The caller
    /// commits or disposes the returned transaction; the lock ends with it.
    /// </summary>
    /// <exception cref="TransactionLockTimeoutException">The lock was not free within the request's timeout.</exception>
    public static async Task<IDbContextTransaction> BeginLockedTransactionAsync(
        this ITransactionLock transactionLock,
        DatabaseFacade database,
        TransactionLockRequest request,
        CancellationToken cancellationToken)
    {
        var transaction = await database.BeginTransactionAsync(cancellationToken);
        try
        {
            await transactionLock.AcquireAsync(database, request, cancellationToken);
            return transaction;
        }
        catch
        {
            // The caller never gets the transaction to dispose. Left open, it would take the scope's later writes
            // and roll them back with the context, and a retry would find the connection already in a transaction.
            await transaction.DisposeAsync();
            throw;
        }
    }
}
