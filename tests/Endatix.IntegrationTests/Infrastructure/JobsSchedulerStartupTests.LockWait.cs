using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
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

    [Fact]
    public async Task Scheduler_whose_role_may_not_update_the_lock_table_fails_startup_saying_so()
    {
        // Arrange — the role can read and write the job tables but not UPDATE Quartz's lock table, which PostgreSQL
        // refuses with 42501 inside Quartz's lock, and Quartz reports as a 25P02.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, "SQL Server reports the missing permission as it is.");
        var ct = TestContext.Current.CancellationToken;
        var role = $"t9_lock_{Guid.NewGuid():N}"[..20];
        const string password = "Lock-Role-Pa55word";
        Func<Task> start;
        var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        try
        {
            await MigrateAsync(database, ct);
            await database.ExecuteAsync($"""
                CREATE ROLE {role} LOGIN PASSWORD '{password}';
                GRANT USAGE ON SCHEMA jobs TO {role};
                GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA jobs TO {role};
                REVOKE UPDATE ON jobs.qrtz_locks FROM {role};
                """, ct);
            var target = new JobsTestNodeDatabase(
                database.Provider,
                new NpgsqlConnectionStringBuilder(database.ConnectionString) { Username = role, Password = password }.ConnectionString);
            await using var node = JobsTestNode.Create(target, migrate: false);

            // Act
            start = () => node.StartAsync(ct);

            // Assert — refused at once, naming the role and the table, without the password.
            var refused = (await start.Should().ThrowAsync<InvalidOperationException>()).Which;
            refused.Message.Should().Contain($"database role '{role}' cannot take Quartz's lock on jobs.qrtz_locks");
            refused.ToString().Should().NotContain(password);
        }
        finally
        {
            // A role is the server's, not the database's: its grants go first, so dropping it leaves nothing behind.
            NpgsqlConnection.ClearAllPools();
            await database.ExecuteAsync($"DROP OWNED BY {role}; DROP ROLE IF EXISTS {role}", CancellationToken.None);
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
