using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;

namespace Endatix.Infrastructure.Identity.Authorization;

/// <summary>
/// Writes a new <see cref="AuthorizationStamp"/> as the last statement before an identity transaction that changed an
/// authorization input commits. Every authenticated request reads the stamp row; on SQL Server without
/// <c>READ_COMMITTED_SNAPSHOT</c> that read waits for a transaction holding the row, so the row is held only for the
/// commit itself, never while the rest of a role change runs.
/// </summary>
internal sealed class AuthorizationStampCommitInterceptor : DbTransactionInterceptor
{
    public static readonly AuthorizationStampCommitInterceptor Instance = new();

    private AuthorizationStampCommitInterceptor()
    {
    }

    public override InterceptionResult TransactionCommitting(
        DbTransaction transaction, TransactionEventData eventData, InterceptionResult result)
    {
        if (TakePendingChange(eventData) is { } context)
        {
            EnsureRowUpdated(Stamp(context).ExecuteUpdate(NewValue()));
        }

        return result;
    }

    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction, TransactionEventData eventData, InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        if (TakePendingChange(eventData) is { } context)
        {
            EnsureRowUpdated(await Stamp(context).ExecuteUpdateAsync(NewValue(), cancellationToken));
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
        if (eventData.Context is not AppIdentityDbContext { AuthorizationStampChangePending: true } context)
        {
            return null;
        }

        context.AuthorizationStampChangePending = false;
        return context;
    }

    // The AddAuthorizationStamp migration seeds the row. Without it no change would reach other instances, so the
    // role change fails rather than commit unseen.
    private static void EnsureRowUpdated(int rows)
    {
        if (rows != 1)
        {
            throw new InvalidOperationException(
                "The authorization stamp row is missing. Apply the AppIdentity migrations (AddAuthorizationStamp).");
        }
    }

    // ExecuteUpdate runs on the context's connection inside the transaction that is about to commit.
    private static IQueryable<AuthorizationStamp> Stamp(AppIdentityDbContext context) =>
        context.AuthorizationStamps.Where(stamp => stamp.Id == AuthorizationStamp.ROW_ID);

    private static Action<UpdateSettersBuilder<AuthorizationStamp>> NewValue()
    {
        var value = Guid.NewGuid();
        return setters => setters.SetProperty(stamp => stamp.Value, value);
    }
}
