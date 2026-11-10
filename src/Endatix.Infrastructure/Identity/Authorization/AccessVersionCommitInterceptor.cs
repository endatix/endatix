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

    private AccessVersionCommitInterceptor()
    {
    }

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

    public override void TransactionCommitted(DbTransaction transaction, TransactionEndEventData eventData) =>
        EndTransaction(eventData);

    public override Task TransactionCommittedAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        TransactionCommitted(transaction, eventData);
        return Task.CompletedTask;
    }

    // A change pending from a transaction that ended without a commit (disposed, or rolled back) is moot.
    public override DbTransaction TransactionStarted(
        DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
    {
        ClearPendingChange(eventData);
        return result;
    }

    public override ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection, TransactionEndEventData eventData, DbTransaction result,
        CancellationToken cancellationToken = default)
    {
        ClearPendingChange(eventData);
        return ValueTask.FromResult(result);
    }

    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) =>
        EndTransaction(eventData);

    public override Task TransactionRolledBackAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        TransactionRolledBack(transaction, eventData);
        return Task.CompletedTask;
    }

    private static AppIdentityDbContext? PendingChange(TransactionEventData eventData) =>
        eventData.Context is AppIdentityDbContext { AccessVersionChangePending: true } context ? context : null;

    private static void ClearPendingChange(TransactionEventData eventData)
    {
        if (eventData.Context is AppIdentityDbContext context)
        {
            context.AccessVersionChangePending = false;
        }
    }

    // Per-transaction state does not outlive the transaction, so a reused context starts clean.
    private static void EndTransaction(TransactionEventData eventData)
    {
        if (eventData.Context is AppIdentityDbContext context)
        {
            context.EndTransaction();
        }
    }

    // The AddAccessVersions migration seeds the system row. Without it no change would reach other instances, so the
    // role change fails rather than commit unseen.
    private static void EnsureRowUpdated(int rows)
    {
        if (rows != 1)
        {
            throw new InvalidOperationException(
                "The system access version row is missing. Apply the AppIdentity migrations (AddAccessVersions).");
        }
    }

    // ExecuteUpdate runs on the context's connection inside the transaction that is about to commit.
    private static IQueryable<AccessVersion> SystemVersion(AppIdentityDbContext context) =>
        context.AccessVersions.Where(version => version.TenantId == AccessVersion.SYSTEM_TENANT_ID);

    private static Action<UpdateSettersBuilder<AccessVersion>> NewValue()
    {
        var value = Guid.NewGuid();
        return setters => setters.SetProperty(version => version.Value, value);
    }
}
