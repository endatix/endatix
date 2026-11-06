using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;

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
            using var command = CreateUpdate(context, transaction);
            EnsureRowUpdated(command.ExecuteNonQuery());
        }

        return result;
    }

    public override async ValueTask<InterceptionResult> TransactionCommittingAsync(
        DbTransaction transaction, TransactionEventData eventData, InterceptionResult result,
        CancellationToken cancellationToken = default)
    {
        if (TakePendingChange(eventData) is { } context)
        {
            await using var command = CreateUpdate(context, transaction);
            EnsureRowUpdated(await command.ExecuteNonQueryAsync(cancellationToken));
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

    private static DbCommand CreateUpdate(AppIdentityDbContext context, DbTransaction transaction)
    {
        var sql = StampSql.For(context);
        var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = $"UPDATE {sql.Table} SET {sql.Value} = @value WHERE {sql.Id} = @id";
        if (context.Database.GetCommandTimeout() is { } timeout)
        {
            command.CommandTimeout = timeout;
        }

        AddParameter(command, "@id", AuthorizationStamp.RowId);
        AddParameter(command, "@value", Guid.NewGuid());
        return command;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    /// <summary>The stamp table and columns, delimited for the context's provider.</summary>
    private sealed record StampSql(string Table, string Id, string Value)
    {
        public static StampSql For(DbContext context)
        {
            var sql = context.GetService<ISqlGenerationHelper>();
            var entity = context.Model.FindEntityType(typeof(AuthorizationStamp))!;
            var table = StoreObjectIdentifier.Table(entity.GetTableName()!, entity.GetSchema());
            string Column(string property) =>
                sql.DelimitIdentifier(entity.FindProperty(property)!.GetColumnName(table)!);

            return new StampSql(
                sql.DelimitIdentifier(table.Name, table.Schema),
                Column(nameof(AuthorizationStamp.Id)),
                Column(nameof(AuthorizationStamp.Value)));
        }
    }
}
