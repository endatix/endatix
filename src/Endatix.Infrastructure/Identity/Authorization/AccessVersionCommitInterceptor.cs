using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Writes a new <see cref="AccessVersion"/> value as the last statement before an identity transaction that changed an
/// authorization input commits. Every authenticated request reads the access version; on SQL Server without
/// <c>READ_COMMITTED_SNAPSHOT</c> that read waits for a transaction holding the row, so the row is held only for the
/// commit itself, never while the rest of a role change runs.
/// </summary>
internal sealed class AccessVersionCommitInterceptor : DbTransactionInterceptor
{
    public static readonly AccessVersionCommitInterceptor Instance = new();

    /// <summary>Stateless; one shared instance serves every context (per-transaction state lives on the context).</summary>
    private AccessVersionCommitInterceptor()
    {
    }

    /// <summary>Writes the new access version as the last statement before a transaction that changed an authorization input commits.</summary>
    public override InterceptionResult TransactionCommitting(
        DbTransaction transaction, TransactionEventData eventData, InterceptionResult result)
    {
        if (PendingChange(eventData) is { } context)
        {
            EnsureRowUpdated(SystemVersion(context).ExecuteUpdate(NewValue()));
            context.AccessVersionChangePending = false;
        }

        return result;
    }

    /// <summary>Writes the new access version as the last statement before a transaction that changed an authorization input commits.</summary>
    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction, TransactionEventData eventData, InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        // The flag clears only after the update succeeds, so a commit retried after a failed update still writes.
        if (PendingChange(eventData) is { } context)
        {
            EnsureRowUpdated(await SystemVersion(context).ExecuteUpdateAsync(NewValue(), cancellationToken));
            context.AccessVersionChangePending = false;
        }

        return result;
    }

    /// <summary>Clears the per-transaction state once the commit has succeeded.</summary>
    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) =>
        EndTransaction(eventData);

    /// <summary>Clears the per-transaction state once the commit has succeeded.</summary>
    public override Task TransactionCommittedAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        TransactionCommitted(transaction, eventData);
        return Task.CompletedTask;
    }

    // A change pending from a transaction that ended without a commit (disposed, or rolled back) is moot.
    /// <summary>Drops a change left pending by a transaction that ended without a commit, so it writes no access version for this one.</summary>
    public override DbTransaction TransactionStarted(
        DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
    {
        ClearPendingChange(eventData);
        return result;
    }

    /// <summary>Drops a change left pending by a transaction that ended without a commit, so it writes no access version for this one.</summary>
    public override ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection, TransactionEndEventData eventData, DbTransaction result,
        CancellationToken cancellationToken = default)
    {
        ClearPendingChange(eventData);
        return ValueTask.FromResult(result);
    }

    /// <summary>Clears the per-transaction state, so a rolled-back change writes no access version.</summary>
    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) =>
        EndTransaction(eventData);

    /// <summary>Clears the per-transaction state, so a rolled-back change writes no access version.</summary>
    public override Task TransactionRolledBackAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        TransactionRolledBack(transaction, eventData);
        return Task.CompletedTask;
    }

    /// <summary>The identity context of this transaction when it has an authorization change to commit; otherwise null.</summary>
    private static AppIdentityDbContext? PendingChange(TransactionEventData eventData) =>
        eventData.Context is AppIdentityDbContext { AccessVersionChangePending: true } context ? context : null;

    /// <summary>Forgets a pending change without touching the rest of the per-transaction state.</summary>
    private static void ClearPendingChange(TransactionEventData eventData)
    {
        if (eventData.Context is AppIdentityDbContext context)
        {
            context.AccessVersionChangePending = false;
        }
    }

    // Per-transaction state does not outlive the transaction, so a reused context starts clean.
    /// <summary>Resets the context's per-transaction state when its transaction commits or rolls back.</summary>
    private static void EndTransaction(TransactionEventData eventData)
    {
        if (eventData.Context is AppIdentityDbContext context)
        {
            context.EndTransaction();
        }
    }

    // The AddAccessVersions migration seeds the system row. Without it no change would reach other instances, so the
    // role change fails rather than commit unseen.
    /// <summary>Fails the commit when the system row is missing, so a role change never commits without reaching other instances.</summary>
    private static void EnsureRowUpdated(int rows)
    {
        if (rows != 1)
        {
            throw new InvalidOperationException(
                "The system access version row is missing. Apply the AppIdentity migrations (AddAccessVersions).");
        }
    }

    // ExecuteUpdate runs on the context's connection inside the transaction that is about to commit.
    /// <summary>The system-roles row, the only row this release reads and writes.</summary>
    private static IQueryable<AccessVersion> SystemVersion(AppIdentityDbContext context) =>
        context.AccessVersions.Where(version => version.TenantId == AccessVersion.SYSTEM_TENANT_ID);

    /// <summary>Sets a fresh random value; a random value never repeats after a restore or a cache flush.</summary>
    private static Action<UpdateSettersBuilder<AccessVersion>> NewValue()
    {
        var value = Guid.NewGuid();
        return setters => setters.SetProperty(version => version.Value, value);
    }
}
