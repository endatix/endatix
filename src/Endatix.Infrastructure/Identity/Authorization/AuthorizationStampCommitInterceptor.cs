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
            if (command.ExecuteNonQuery() == 0)
            {
                using var insert = CreateInsert(context, transaction);
                insert.ExecuteNonQuery();
            }
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
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                await using var insert = CreateInsert(context, transaction);
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        return result;
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

    private static DbCommand CreateUpdate(AppIdentityDbContext context, DbTransaction transaction)
    {
        var sql = StampSql.For(context);
        return CreateCommand(transaction, $"UPDATE {sql.Table} SET {sql.Value} = @value WHERE {sql.Id} = @id");
    }

    // The row is seeded by the migration; a database reset (integration tests) removes it.
    private static DbCommand CreateInsert(AppIdentityDbContext context, DbTransaction transaction)
    {
        var sql = StampSql.For(context);
        return CreateCommand(transaction, $"INSERT INTO {sql.Table} ({sql.Id}, {sql.Value}) VALUES (@id, @value)");
    }

    private static DbCommand CreateCommand(DbTransaction transaction, string commandText)
    {
        var command = transaction.Connection!.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = commandText;
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
