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
        var name = new NpgsqlConnectionStringBuilder(database.ConnectionString).Database;
        await database.ExecuteAsync($"ALTER DATABASE {name} SET lock_timeout = '1s'", ct);
        await using var holder = new NpgsqlConnection(database.ConnectionString);
        await holder.OpenAsync(ct);
        await using var holding = await holder.BeginTransactionAsync(ct);
        await using (var hold = new NpgsqlCommand("SELECT * FROM jobs.qrtz_LOCKS WHERE LOCK_NAME = 'TRIGGER_ACCESS' FOR UPDATE", holder, holding))
        {
            await hold.ExecuteNonQueryAsync(ct);
        }

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
            () => Task.FromResult(logs.Entries.Any(entry => entry.Level == LogLevel.Warning && entry.Message.Contains(LostRaceWarning))),
            TimeSpan.FromSeconds(30),
            ct);
        await holding.CommitAsync(ct);
        var jobId = await node.EnqueueAsync(BackgroundJobRequest.Create(new ProbePayload(), tenantId: 5), ct);
        var ran = await JobsTestWait.UntilAsync(() => Task.FromResult(invocations.CountFor(jobId) == 1), Recovery, ct);

        // Assert — retried as a lost race, then started with nothing to fix.
        retried.Should().BeTrue();
        ran.Should().BeTrue();
        logs.Entries.Should().NotContain(entry => entry.Level == LogLevel.Critical);
    }
}
