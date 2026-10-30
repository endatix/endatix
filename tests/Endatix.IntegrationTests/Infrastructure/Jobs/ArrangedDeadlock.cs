using System.Data.Common;
using System.Diagnostics;
using Endatix.IntegrationTests.Shared;
using Microsoft.Data.SqlClient;
using Npgsql;
using Quartz.Impl.AdoJobStore;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>
/// A real deadlock between an enqueue's transaction and one of the test's own, with the database choosing the
/// enqueue's as its victim: SQL Server raises 1205 and has already rolled the transaction back, PostgreSQL raises
/// 40P01 and aborts it.
/// </summary>
/// <remarks>
/// The other transaction locks row B and then waits for row A; the enqueue's locks A, waits until the other is
/// blocked on it, and then asks for B. The victim is picked by priority: SQL Server's <c>DEADLOCK_PRIORITY</c>, and
/// on PostgreSQL the shorter <c>deadlock_timeout</c>, whose session runs the deadlock check first.
/// </remarks>
internal sealed class ArrangedDeadlock : IAsyncDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(20);

    private readonly JobsTestDatabase _database;
    private readonly TaskCompletionSource _otherHoldsB = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _enqueueHoldsA = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task? _other;

    private ArrangedDeadlock(JobsTestDatabase database) => _database = database;

    /// <summary>What the database raised in the enqueue's transaction.</summary>
    public Exception? Victim { get; private set; }

    public static async Task<ArrangedDeadlock> PrepareAsync(JobsTestDatabase database, CancellationToken ct)
    {
        await database.ExecuteAsync(
            """
            CREATE TABLE deadlock_a (id int PRIMARY KEY);
            CREATE TABLE deadlock_b (id int PRIMARY KEY);
            INSERT INTO deadlock_a VALUES (1);
            INSERT INTO deadlock_b VALUES (1);
            """,
            ct);
        var deadlock = new ArrangedDeadlock(database);
        deadlock._other = deadlock.HoldBThenWaitForAAsync(ct);
        return deadlock;
    }

    /// <summary>Runs on the enqueue's enlisted connection, inside its transaction, and throws what the database raises.</summary>
    public async Task DeadlockAsync(ConnectionAndTransactionHolder enqueue, CancellationToken ct)
    {
        await _otherHoldsB.Task.WaitAsync(Patience, ct);
        var session = new Session(enqueue.Connection, enqueue.Transaction);
        await session.ExecuteAsync(LowPriority, ct);
        await session.ExecuteAsync("UPDATE deadlock_a SET id = id", ct);
        _enqueueHoldsA.SetResult();
        await WaitUntilTheOtherWaitsForAAsync(ct);
        try
        {
            await session.ExecuteAsync("UPDATE deadlock_b SET id = id", ct);
        }
        catch (DbException victim)
        {
            Victim = victim;
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        _enqueueHoldsA.TrySetResult();
        await (_other ?? Task.CompletedTask);
    }

    private string LowPriority => _database.Provider == TestDatabaseProvider.PostgreSql
        ? "SET LOCAL deadlock_timeout = '100ms'"
        : "SET DEADLOCK_PRIORITY LOW";

    private string HighPriority => _database.Provider == TestDatabaseProvider.PostgreSql
        ? "SET LOCAL deadlock_timeout = '60s'"
        : "SET DEADLOCK_PRIORITY HIGH";

    // The test's transaction: it wins, so it commits once the enqueue's is rolled back.
    private async Task HoldBThenWaitForAAsync(CancellationToken ct)
    {
        await using var connection = Connect();
        await connection.OpenAsync(ct);
        await using var transaction = await connection.BeginTransactionAsync(ct);
        var session = new Session(connection, transaction);
        await session.ExecuteAsync(HighPriority, ct);
        await session.ExecuteAsync("UPDATE deadlock_b SET id = id", ct);
        _otherHoldsB.SetResult();
        await _enqueueHoldsA.Task.WaitAsync(Patience, ct);
        await session.ExecuteAsync("UPDATE deadlock_a SET id = id", ct);
        await transaction.CommitAsync(ct);
    }

    private async Task WaitUntilTheOtherWaitsForAAsync(CancellationToken ct)
    {
        var waited = Stopwatch.StartNew();
        while (await _database.CountAsync(WaitingForA, ct) == 0)
        {
            if (waited.Elapsed > Patience)
            {
                throw new TimeoutException("The test's transaction never waited for row A.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(50), ct);
        }
    }

    private string WaitingForA => _database.Provider == TestDatabaseProvider.PostgreSql
        ? "SELECT count(*) FROM pg_stat_activity WHERE wait_event_type = 'Lock' AND query LIKE 'UPDATE deadlock_a%'"
        : """
          SELECT count(*) FROM sys.dm_exec_requests r CROSS APPLY sys.dm_exec_sql_text(r.sql_handle) t
          WHERE r.wait_type LIKE 'LCK%' AND t.text LIKE 'UPDATE deadlock_a%'
          """;

    private DbConnection Connect() => _database.Provider == TestDatabaseProvider.PostgreSql
        ? new NpgsqlConnection(_database.ConnectionString)
        : new SqlConnection(_database.ConnectionString);

    // A connection, and the transaction its statements run in.
    private readonly record struct Session(DbConnection Connection, DbTransaction? Transaction)
    {
        public async Task ExecuteAsync(string sql, CancellationToken ct)
        {
            await using var command = Connection.CreateCommand();
            command.Transaction = Transaction;
            command.CommandText = sql;
            await command.ExecuteNonQueryAsync(ct);
        }
    }
}
