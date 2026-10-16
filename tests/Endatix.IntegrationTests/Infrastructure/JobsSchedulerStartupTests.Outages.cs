using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Microsoft.Extensions.Logging;
using Npgsql;
using Quartz;
using Quartz.Impl.AdoJobStore;

namespace Endatix.IntegrationTests;

/// <summary>
/// A start whose build fails in a way that hides or changes why: a PostgreSQL 25P02, and an outage that changes kind.
/// </summary>
public sealed partial class JobsSchedulerStartupTests
{
    private const string UnreachableStartError = "The background jobs scheduler could not reach its database";

    [Fact]
    public async Task Scheduler_whose_build_meets_an_aborted_transaction_on_a_database_turned_read_only_waits_saying_so()
    {
        // Arrange — the probe found the database writable, then Quartz's connection landed on a read-only session, as
        // during a switchover: Quartz reports only the 25P02 that followed the 25006 inside its lock.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, "25P02 and read-only sessions are PostgreSQL's.");
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        var schedulers = new SchedulerFactoryFailingOnce(async () =>
        {
            await database.SetReadOnlyAsync(true, ct);
            return AbortedTransaction();
        });
        var logs = new CapturingLoggerProvider();
        using var service = HostedServiceOver(new(database.Provider, database.ConnectionString), schedulers, logs);

        // Act
        var start = () => service.StartAsync(ct);

        // Assert — waited out and named, not a failed start.
        await start.Should().NotThrowAsync();
        await service.StopAsync(ct);
        LoggedError(logs, ReadOnlyStartError).Should().BeTrue();
    }

    [Fact]
    public async Task Scheduler_whose_build_meets_an_aborted_transaction_the_probe_cannot_explain_fails_startup()
    {
        // Arrange — the database is writable and reachable, so nothing explains the 25P02: a refusal, as before.
        var ct = TestContext.Current.CancellationToken;
        var schedulers = new SchedulerFactoryFailingOnce(() => Task.FromResult<Exception>(AbortedTransaction()));
        var logs = new CapturingLoggerProvider();
        using var service = HostedServiceOver(new(fixture.Provider, fixture.ConnectionString), schedulers, logs);

        // Act
        var start = () => service.StartAsync(ct);

        // Assert
        await start.Should().ThrowAsync<LockException>();
    }

    [Fact]
    public async Task Scheduler_whose_outage_changes_kind_logs_the_new_kind_at_once()
    {
        // Arrange — the first build meets a failover, the second a read-only database; the third succeeds.
        var ct = TestContext.Current.CancellationToken;
        var scheduler = Substitute.For<IScheduler>();
        var schedulers = Substitute.For<ISchedulerFactory>();
        schedulers.GetScheduler(Arg.Any<CancellationToken>()).Returns(
            _ => throw new SchedulerException(
                "The store failed.",
                new PostgresException("terminating connection due to administrator command", "FATAL", "FATAL", PostgresErrorCodes.AdminShutdown)),
            _ => throw new SchedulerException(
                "The store failed.",
                new PostgresException("cannot execute INSERT in a read-only transaction", "ERROR", "ERROR", PostgresErrorCodes.ReadOnlySqlTransaction)),
            _ => ValueTask.FromResult(scheduler));
        var logs = new CapturingLoggerProvider();
        using var service = HostedServiceOver(new(fixture.Provider, fixture.ConnectionString), schedulers, logs);

        // Act
        await service.StartAsync(ct);
        var started = await JobsTestWait.UntilAsync(
            () => Task.FromResult(scheduler.ReceivedCalls().Any(call => call.GetMethodInfo().Name == nameof(IScheduler.Start))),
            TimeSpan.FromSeconds(20),
            ct);
        await service.StopAsync(ct);

        // Assert — the second try's read-only database is logged then, not twelve tries later.
        started.Should().BeTrue();
        LoggedError(logs, UnreachableStartError).Should().BeTrue();
        LoggedError(logs, ReadOnlyStartError).Should().BeTrue();
    }

    // What Quartz 4.2.2 threw at a start on a read-only PostgreSQL database, measured.
    private static LockException AbortedTransaction() => new(
        "Failure obtaining db row lock: 25P02: current transaction is aborted, commands ignored until end of transaction block",
        new PostgresException(
            "current transaction is aborted, commands ignored until end of transaction block",
            "ERROR",
            "ERROR",
            PostgresErrorCodes.InFailedSqlTransaction));
}
