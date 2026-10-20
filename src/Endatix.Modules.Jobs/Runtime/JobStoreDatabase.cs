using System.Data.Common;
using Endatix.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Npgsql;

namespace Endatix.Modules.Jobs.Runtime;

/// <summary>
/// The database the scheduler's tables live in, beside the job rows, and what the scheduler checks of it before it
/// starts.
/// </summary>
/// <param name="connectionString">The host's default connection string.</param>
/// <param name="isPostgreSql">Whether that database is PostgreSQL; otherwise it is SQL Server.</param>
/// <param name="locksOnInsert">
/// Whether enqueueing holds the cluster-wide trigger lock until it commits, as Quartz's store was configured with it.
/// </param>
/// <remarks>
/// <para>
/// <see cref="LocksOnInsert"/> is decided once, from configuration at registration, because that is when Quartz's
/// store takes it and Quartz cannot change it afterwards: an options callback a host adds in code later reaches
/// neither the store nor the snapshot check, so the two can never disagree.
/// </para>
/// <para>
/// A class, not a record: a record's generated <c>ToString</c> would print the connection string, password included,
/// into any log, debugger view or assertion message that shows this singleton.
/// </para>
/// </remarks>
internal sealed class JobStoreDatabase(string connectionString, bool isPostgreSql, bool locksOnInsert)
{
    // The table Quartz takes its lock on, as PostgreSQL names it.
    private const string PostgreSqlLockTable = QuartzRegistration.TablePrefix + "locks";

    /// <summary>The connection string; never printed (see <see cref="ToString"/>).</summary>
    public string ConnectionString { get; } = connectionString;

    public bool IsPostgreSql { get; } = isPostgreSql;

    public bool LocksOnInsert { get; } = locksOnInsert;

    private const string LockOnInsertKey = $"{BackgroundJobsOptions.SectionName}:{nameof(BackgroundJobsOptions.LockOnInsert)}";

    /// <summary>The store the host's configuration describes.</summary>
    /// <remarks>
    /// On PostgreSQL an uncommitted trigger is invisible to every node's acquisition, so enqueue need not hold the
    /// lock until it commits. On SQL Server, unless the database reads committed snapshots, an acquisition reading the
    /// triggers waits on every open enqueue's uncommitted rows, which is what Quartz takes the lock on insert for:
    /// holding it makes enqueues and acquisitions take turns. A host whose database reads committed snapshots may turn
    /// it off, and a host may set it on either database.
    /// </remarks>
    public static JobStoreDatabase From(IConfiguration configuration, BackgroundJobsOptions options)
    {
        var isPostgreSql = DatabaseProviderResolver.IsPostgreSql(configuration);
        return new JobStoreDatabase(
            ModuleDesignTimeConfiguration.GetDefaultConnectionString(configuration),
            isPostgreSql,
            options.LockOnInsert ?? !isPostgreSql);
    }

    /// <summary>Names the provider and the database, never the connection string or its credentials.</summary>
    public override string ToString() =>
        $"{nameof(JobStoreDatabase)} {{ {(IsPostgreSql ? "PostgreSQL" : "SQL Server")} database '{DatabaseName}', " +
        $"{nameof(LocksOnInsert)} = {LocksOnInsert} }}";

    /// <summary>The database the connection string names, for messages; never the credentials.</summary>
    public string DatabaseName => IsPostgreSql
        ? new NpgsqlConnectionStringBuilder(ConnectionString).Database ?? ""
        : new SqlConnectionStringBuilder(ConnectionString).InitialCatalog;

    /// <summary>
    /// Why the scheduler cannot start on the database for now, or <see langword="null"/> when it can: the connection
    /// does not open because the database is away, or, on PostgreSQL, it opens but every new transaction is read-only
    /// (a 25006 <see cref="PostgresException"/>), as on a standby. A failure the database answered and refused with,
    /// such as a login it does not accept, is thrown: waiting never fixes it.
    /// </summary>
    public async Task<Exception?> UnreachableBecauseAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = Connect();
            await connection.OpenAsync(cancellationToken);
            return IsPostgreSql && await TransactionsAreReadOnlyAsync(connection, cancellationToken)
                ? ReadOnlyTransactions()
                : null;
        }
        catch (Exception exception) when (DatabaseFailures.IsAwayAtStartup(exception))
        {
            return exception;
        }
    }

    /// <summary>
    /// Why a SQL Server store that enqueues without the trigger lock may not run on this database, which does not read
    /// committed snapshots, or <see langword="null"/> when it may.
    /// </summary>
    /// <remarks>
    /// Without snapshots, an acquisition waits on every enqueue's uncommitted triggers while that enqueue holds no
    /// lock to take turns by, so a long enqueue transaction stalls every node's acquisition. The refusal is returned,
    /// not thrown, so a caller can tell it from a failure to ask the database.
    /// </remarks>
    public async Task<string?> ReadCommittedSnapshotRefusalAsync(CancellationToken cancellationToken)
    {
        if (IsPostgreSql || LocksOnInsert || await ReadsCommittedSnapshotsAsync(cancellationToken))
        {
            return null;
        }

        return $"The background jobs scheduler cannot start: '{LockOnInsertKey}' is false, and on SQL Server that " +
            "needs READ_COMMITTED_SNAPSHOT on for the database, which is off. Turn it on " +
            "(ALTER DATABASE CURRENT SET READ_COMMITTED_SNAPSHOT ON WITH ROLLBACK IMMEDIATE), or remove " +
            $"'{LockOnInsertKey}' so that enqueueing takes the trigger lock.";
    }

    private async Task<bool> ReadsCommittedSnapshotsAsync(CancellationToken cancellationToken)
    {
        await using var connection = Connect();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT is_read_committed_snapshot_on FROM sys.databases WHERE name = DB_NAME()";
        return await command.ExecuteScalarAsync(cancellationToken) is true;
    }

    /// <summary>
    /// Why this connection's role may not take Quartz's lock on PostgreSQL, or <see langword="null"/> when it may, or
    /// on SQL Server, or when the lock table is not there yet.
    /// </summary>
    /// <remarks>
    /// Quartz takes its lock with <c>SELECT … FOR UPDATE</c> on <c>jobs.qrtz_locks</c> and inserts the row when it is
    /// missing, so the role needs SELECT, UPDATE and INSERT on it. Without them PostgreSQL refuses with 42501, which
    /// Quartz 4.2.2 reports as the 25P02 of its next statement (measured), so only asking tells the refusal from a lost
    /// race. SQL Server reports a missing permission there as it is (229, measured), so it is not asked.
    /// </remarks>
    public async Task<string?> LockTableRefusalAsync(CancellationToken cancellationToken)
    {
        if (!IsPostgreSql)
        {
            return null;
        }

        await using var connection = Connect();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT to_regclass('{PostgreSqlLockTable}') IS NOT NULL
                AND NOT (has_table_privilege('{PostgreSqlLockTable}', 'SELECT')
                    AND has_table_privilege('{PostgreSqlLockTable}', 'UPDATE')
                    AND has_table_privilege('{PostgreSqlLockTable}', 'INSERT'))
            """;
        return await command.ExecuteScalarAsync(cancellationToken) is true
            ? $"The background jobs scheduler cannot start: database role '{new NpgsqlConnectionStringBuilder(ConnectionString).Username}' " +
              $"cannot take Quartz's lock on {PostgreSqlLockTable}. Grant it SELECT, UPDATE and INSERT on that table."
            : null;
    }

    // A read-only PostgreSQL database, a standby or one with default_transaction_read_only on, refuses the scheduler's
    // first write with 25006 inside the lock Quartz takes, and Quartz then reports only the 25P02 ("current transaction
    // is aborted") of its next statement, which says nothing of why. So the probe asks; SQL Server's 3906 reaches the
    // start as it is.
    private static async Task<bool> TransactionsAreReadOnlyAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SHOW transaction_read_only";
        return await command.ExecuteScalarAsync(cancellationToken) is "on";
    }

    // Said as PostgreSQL says a write it refused for the same reason, so the start treats both alike.
    private PostgresException ReadOnlyTransactions() => new(
        $"Every new transaction on database '{DatabaseName}' is read-only: it is a standby, or default_transaction_read_only is on.",
        "ERROR",
        "ERROR",
        PostgresErrorCodes.ReadOnlySqlTransaction);

    private DbConnection Connect() =>
        IsPostgreSql ? new NpgsqlConnection(ConnectionString) : new SqlConnection(ConnectionString);
}
