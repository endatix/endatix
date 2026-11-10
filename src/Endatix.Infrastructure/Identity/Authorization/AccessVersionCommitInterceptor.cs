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
        if (TakePendingChange(eventData) is { } context)
        {
            EnsureRowUpdated(SystemVersion(context).ExecuteUpdate(NewValue()));
        }

        return result;
    }

    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction, TransactionEventData eventData, InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        if (TakePendingChange(eventData) is { } context)
        {
            EnsureRowUpdated(await SystemVersion(context).ExecuteUpdateAsync(NewValue(), cancellationToken));
        }

        return result;
    }

    // A change pending from a transaction that ended without a commit (disposed, or rolled back) is moot.
    public override DbTransaction TransactionStarted(
        DbConnection connection, TransactionEndEventData eventData, DbTransaction result)
    {
        TakePendingChange(eventData);
        return result;
    }

    public override ValueTask<DbTransaction> TransactionStartedAsync(
        DbConnection connection, TransactionEndEventData eventData, DbTransaction result,
        CancellationToken cancellationToken = default)
    {
        TakePendingChange(eventData);
        return ValueTask.FromResult(result);
    }

    public override void TransactionRolledBack(DbTransaction transaction, TransactionEndEventData eventData) =>
        TakePendingChange(eventData);

    public override Task TransactionRolledBackAsync(
        DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        TakePendingChange(eventData);
        return Task.CompletedTask;
    }

    private static AppIdentityDbContext? TakePendingChange(TransactionEventData eventData)
    {
        if (eventData.Context is not AppIdentityDbContext { AccessVersionChangePending: true } context)
        {
            return null;
        }

        context.AccessVersionChangePending = false;
        return context;
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
