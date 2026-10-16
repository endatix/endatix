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
/// <param name="ConnectionString">The host's default connection string.</param>
/// <param name="IsPostgreSql">Whether that database is PostgreSQL; otherwise it is SQL Server.</param>
/// <param name="LocksOnInsert">
/// Whether enqueueing holds the cluster-wide trigger lock until it commits, as Quartz's store was configured with it.
/// </param>
/// <remarks>
/// <paramref name="LocksOnInsert"/> is decided once, from configuration at registration, because that is when Quartz's
/// store takes it and Quartz cannot change it afterwards: an options callback a host adds in code later reaches
/// neither the store nor the snapshot check, so the two can never disagree.
/// </remarks>
internal sealed record JobStoreDatabase(string ConnectionString, bool IsPostgreSql, bool LocksOnInsert)
{
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

    /// <summary>The database the connection string names, for messages; never the credentials.</summary>
    public string DatabaseName => IsPostgreSql
        ? new NpgsqlConnectionStringBuilder(ConnectionString).Database ?? ""
        : new SqlConnectionStringBuilder(ConnectionString).InitialCatalog;

    /// <summary>
    /// Why a connection does not open when the database is away, or <see langword="null"/> when it opens. A failure
    /// the database answered and refused with, such as a login it does not accept, is thrown: waiting never fixes it.
    /// </summary>
    public async Task<Exception?> UnreachableBecauseAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = Connect();
            await connection.OpenAsync(cancellationToken);
            return null;
        }
        catch (Exception exception) when (DatabaseFailures.IsUnreachable(exception))
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

    private DbConnection Connect() =>
        IsPostgreSql ? new NpgsqlConnection(ConnectionString) : new SqlConnection(ConnectionString);
}
