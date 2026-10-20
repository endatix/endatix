using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Endatix.IntegrationTests;

/// <summary>A start that waits on Quartz's lock row while another session holds it, past PostgreSQL's lock_timeout.</summary>
public sealed partial class JobsSchedulerStartupTests
{
    [Fact]
    public async Task Scheduler_whose_lock_row_is_held_past_the_lock_timeout_retries_as_contention_and_starts()
    {
        // Arrange — a session holds the trigger lock row, and the database's lock_timeout is short, so the start's lock
        // wait times out (55P03) inside Quartz's lock handler, which reports the 25P02 of its next statement instead.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, "lock_timeout and 25P02 are PostgreSQL's.");
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        await MigrateAsync(database, ct);
        await using var held = await HeldTriggerLock.TakeAsync(database, ct);
        var invocations = new ProbeInvocations();
        var logs = new CapturingLoggerProvider();
        await using var node = JobsTestNode.Create(
            database,
            configureServices: services =>
            {
                services.AddProbe(invocations);
                services.AddSingleton<ILoggerProvider>(logs);
            },
            migrate: false);

        // Act
        await node.StartAsync(ct);
        var retried = await JobsTestWait.UntilAsync(
            () => Task.FromResult(logs.Entries.Any(entry => entry.Level == LogLevel.Warning && entry.Message.Contains(HiddenCauseWarning))),
            TimeSpan.FromSeconds(30),
            ct);
        await held.ReleaseAsync(ct);
        var jobId = await node.EnqueueAsync(BackgroundJobRequest.Create(new ProbePayload(), tenantId: 5), ct);
        var ran = await JobsTestWait.UntilAsync(() => Task.FromResult(invocations.CountFor(jobId) == 1), Recovery, ct);

        // Assert — retried as a lost race, then started with nothing to fix.
        retried.Should().BeTrue();
        ran.Should().BeTrue();
        logs.Entries.Should().NotContain(entry => entry.Level == LogLevel.Critical);
    }

    // What a role may do with Quartz's lock table, granted beyond full access to the other job tables, and whether the
    // start should refuse it.
    public static TheoryData<string, string, bool> LockTableGrants() => new()
    {
        { "no UPDATE", "REVOKE UPDATE ON jobs.qrtz_locks FROM {0};", true },
        { "UPDATE on one column", "REVOKE UPDATE ON jobs.qrtz_locks FROM {0}; GRANT UPDATE (lock_name) ON jobs.qrtz_locks TO {0};", false },
        { "no INSERT, lock rows there", "REVOKE INSERT ON jobs.qrtz_locks FROM {0};" + LockRows, false },
        { "no INSERT, lock rows missing", "REVOKE INSERT ON jobs.qrtz_locks FROM {0}; DELETE FROM jobs.qrtz_locks;", true },
    };

    // Both of the scheduler's lock rows, as Quartz inserts them the first time it takes each lock.
    private const string LockRows =
        " INSERT INTO jobs.qrtz_locks (sched_name, lock_name) VALUES ('endatix-jobs', 'TRIGGER_ACCESS'), ('endatix-jobs', 'STATE_ACCESS') ON CONFLICT DO NOTHING;";

    [Theory]
    [MemberData(nameof(LockTableGrants))]
    public async Task Scheduler_started_by_a_role_that_may_not_take_the_lock_fails_saying_so_and_one_that_may_starts(
        string grants,
        string grantSql,
        bool refused)
    {
        // Arrange — PostgreSQL refuses a role inside Quartz's lock with 42501, which Quartz reports as a 25P02; the start
        // asks for the role's privileges, and a column's UPDATE, or INSERT once the rows exist, is all the lock needs.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, "SQL Server reports a missing permission as it is.");
        var ct = TestContext.Current.CancellationToken;
        var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        await using var role = new LockTableRole(database);
        await MigrateAsync(database, ct);
        await role.CreateAsync(grantSql, ct);
        await using var node = JobsTestNode.Create(new JobsTestNodeDatabase(database.Provider, role.ConnectionString), migrate: false);

        // Act
        var check = await new JobStoreDatabase(role.ConnectionString, isPostgreSql: true, locksOnInsert: false).LockTableRefusalAsync(ct);
        var start = () => node.StartAsync(ct);

        // Assert — refused at once, naming the role and the table without the password, or started.
        if (refused)
        {
            check.Should().Contain($"database role '{role.Name}' cannot take Quartz's lock on jobs.qrtz_locks", grants);
            var failure = (await start.Should().ThrowAsync<InvalidOperationException>(grants)).Which;
            failure.Message.Should().Be(check);
            failure.ToString().Should().NotContain(LockTableRole.Password);
        }
        else
        {
            check.Should().BeNull(grants);
            await start.Should().NotThrowAsync(grants);
        }
    }

    // A PostgreSQL role with full access to the job tables but what a case grants on the lock table, and the database
    // it was made for, both dropped with it: a role is the server's, so its grants go first.
    private sealed class LockTableRole(JobsTestDatabase database) : IAsyncDisposable
    {
        public const string Password = "Lock-Role-Pa55word";

        public string Name { get; } = $"t9_lock_{Guid.NewGuid():N}"[..20];

        public string ConnectionString =>
            new NpgsqlConnectionStringBuilder(database.ConnectionString) { Username = Name, Password = Password }.ConnectionString;

        public Task CreateAsync(string grantSql, CancellationToken ct) => database.ExecuteAsync($"""
            CREATE ROLE {Name} LOGIN PASSWORD '{Password}';
            GRANT USAGE ON SCHEMA jobs TO {Name};
            GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA jobs TO {Name};
            {string.Format(System.Globalization.CultureInfo.InvariantCulture, grantSql, Name)}
            """, ct);

        public async ValueTask DisposeAsync()
        {
            NpgsqlConnection.ClearAllPools();
            await database.ExecuteAsync($"DO $$ BEGIN IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = '{Name}') THEN DROP OWNED BY {Name}; DROP ROLE {Name}; END IF; END $$", CancellationToken.None);
            await database.DisposeAsync();
        }
    }

    // Quartz's trigger lock row, held by a session of its own, on a database whose other sessions wait at most a second.
    private sealed class HeldTriggerLock : IAsyncDisposable
    {
        private readonly NpgsqlConnection _connection;
        private readonly NpgsqlTransaction _transaction;

        private HeldTriggerLock(NpgsqlConnection connection, NpgsqlTransaction transaction)
        {
            _connection = connection;
            _transaction = transaction;
        }

        public static async Task<HeldTriggerLock> TakeAsync(JobsTestDatabase database, CancellationToken ct)
        {
            var name = new NpgsqlConnectionStringBuilder(database.ConnectionString).Database;
            await database.ExecuteAsync($"ALTER DATABASE {name} SET lock_timeout = '1s'", ct);
            var connection = new NpgsqlConnection(database.ConnectionString);
            await connection.OpenAsync(ct);
            var transaction = await connection.BeginTransactionAsync(ct);
            await using var hold = new NpgsqlCommand(
                "SELECT * FROM jobs.qrtz_LOCKS WHERE LOCK_NAME = 'TRIGGER_ACCESS' FOR UPDATE", connection, transaction);
            await hold.ExecuteNonQueryAsync(ct);
            return new HeldTriggerLock(connection, transaction);
        }

        public Task ReleaseAsync(CancellationToken ct) => _transaction.CommitAsync(ct);

        public async ValueTask DisposeAsync()
        {
            await _transaction.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }
}
