using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Writes new <see cref="AccessVersion"/> values, for the tenants whose authorization inputs the transaction changed, as
/// the last statements before an identity transaction commits. Every authenticated request reads its tenant's access
/// version; on SQL Server without <c>READ_COMMITTED_SNAPSHOT</c> that read waits for a transaction holding the row, so
/// the row is held only for the commit itself, never while the rest of a role change runs.
/// </summary>
internal sealed class AccessVersionCommitInterceptor : DbTransactionInterceptor
{
    public static readonly AccessVersionCommitInterceptor Instance = new();

    /// <summary>Stateless; one shared instance serves every context (per-transaction state lives on the context).</summary>
    private AccessVersionCommitInterceptor()
    {
    }

    /// <summary>Writes the new access versions as the last statements before a transaction that changed an authorization input commits.</summary>
    public override InterceptionResult TransactionCommitting(
        DbTransaction transaction, TransactionEventData eventData, InterceptionResult result)
    {
        if (PendingChange(eventData) is { } context)
        {
            Store(context).Change(context.PendingAccessVersionRows);
            context.ResetAccessVersionState();
        }

        return result;
    }

    /// <inheritdoc cref="TransactionCommitting"/>
    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction, TransactionEventData eventData, InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        // The state clears only after the write succeeds, so a commit retried after a failed write still writes.
        if (PendingChange(eventData) is { } context)
        {
            await Store(context).ChangeAsync(context.PendingAccessVersionRows, cancellationToken);
            context.ResetAccessVersionState();
        }

        return result;
    }

    /// <summary>Clears the per-transaction state once the commit has succeeded.</summary>
    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) =>
        Reset(eventData);

    /// <inheritdoc cref="TransactionCommitted"/>
    public override Task TransactionCommittedAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        TransactionCommitted(transaction, eventData);
        return Task.CompletedTask;
    }

    /// <summary>Drops a change left pending by a transaction that ended without a commit, so it writes no access version for this one.</summary>
    public override DbTransaction TransactionStarted(
        DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
    {
        Reset(eventData);
        return result;
    }

    /// <inheritdoc cref="TransactionStarted"/>
    public override ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection, TransactionEndEventData eventData, DbTransaction result,
        CancellationToken cancellationToken = default)
    {
        Reset(eventData);
        return ValueTask.FromResult(result);
    }

    /// <summary>Clears the per-transaction state, so a rolled-back change writes no access version.</summary>
    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) =>
        Reset(eventData);

    /// <inheritdoc cref="TransactionRolledBack"/>
    public override Task TransactionRolledBackAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        TransactionRolledBack(transaction, eventData);
        return Task.CompletedTask;
    }

    /// <summary>The identity context of this transaction when it has an authorization change to commit; otherwise null.</summary>
    private static AppIdentityDbContext? PendingChange(TransactionEventData eventData) =>
        eventData.Context is AppIdentityDbContext { PendingAccessVersionRows.Count: > 0 } context ? context : null;

    /// <summary>
    /// The database store, the only one that can write inside the committing transaction. A store kept outside the
    /// identity database (endatix#1637) writes after the commit instead, so it hooks <see cref="TransactionCommitted"/>.
    /// </summary>
    private static DatabaseAccessVersionStore Store(AppIdentityDbContext context) => new(context);

    /// <summary>Resets the context's per-transaction state; the context owns the one reset.</summary>
    private static void Reset(TransactionEventData eventData)
    {
        if (eventData.Context is AppIdentityDbContext context)
        {
            context.ResetAccessVersionState();
        }
    }
}
