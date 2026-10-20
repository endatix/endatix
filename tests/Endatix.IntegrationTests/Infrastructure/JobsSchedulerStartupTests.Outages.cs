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
    private const string HiddenCauseWarning = "met a failed transaction inside Quartz's lock while starting";

    // As many tries as the scheduler makes before it says a 25P02 it keeps meeting is likely a refusal.
    private const int RunOfUnexplainedAborts = 12;

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
    public async Task Scheduler_whose_build_meets_an_aborted_transaction_the_probe_cannot_explain_retries_as_contention_and_starts()
    {
        // Arrange — the database is writable and reachable, so nothing the probe sees explains the 25P02: a lost race
        // inside Quartz's lock, such as a lock wait past lock_timeout, which clears once the other transaction ends.
        var ct = TestContext.Current.CancellationToken;

        // Act
        var (started, logs) = await StartAfterAFailedBuildAsync(AbortedTransaction(), ct);

        // Assert
        started.Should().BeTrue();
        logs.Entries.Should().Contain(entry => entry.Level == LogLevel.Warning && entry.Message.Contains(HiddenCauseWarning));
        logs.Entries.Should().NotContain(entry => entry.Level == LogLevel.Critical);
    }

    [Fact]
    public async Task Scheduler_whose_build_meets_unexplained_aborted_transactions_twelve_times_in_a_row_says_so_at_error()
    {
        // Arrange — the role may take the lock, so the 25P02 is contention, which here keeps coming back for twelve tries,
        // as a lock held for long would.
        var ct = TestContext.Current.CancellationToken;
        var scheduler = Substitute.For<IScheduler>();
        var tries = 0;
        var schedulers = Substitute.For<ISchedulerFactory>();
        schedulers.GetScheduler(Arg.Any<CancellationToken>()).Returns(_ =>
            ++tries <= RunOfUnexplainedAborts ? throw AbortedTransaction() : ValueTask.FromResult(scheduler));
        var logs = new CapturingLoggerProvider();
        using var service = HostedServiceOver(new(fixture.Provider, fixture.ConnectionString), schedulers, logs);

        // Act
        await service.StartAsync(ct);
        var started = await JobsTestWait.UntilAsync(
            () => Task.FromResult(scheduler.ReceivedCalls().Any(call => call.GetMethodInfo().Name == nameof(IScheduler.Start))),
            TimeSpan.FromSeconds(90),
            ct);
        await service.StopAsync(ct);

        // Assert — warned on the first, raised to an error on the twelfth, and started once it cleared.
        started.Should().BeTrue();
        logs.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Warning && entry.Message.Contains(HiddenCauseWarning));
        logs.Entries.Should().ContainSingle(entry => entry.Level == LogLevel.Error
            && entry.Message.Contains($"on {RunOfUnexplainedAborts} tries in a row")
            && entry.Message.Contains("contention that does not clear"));
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

    [Fact]
    public async Task Scheduler_whose_outage_alternates_between_kinds_logs_each_kind_once_not_every_try()
    {
        // Arrange — during a failover the start alternates between an unreachable database and a read-only one.
        var ct = TestContext.Current.CancellationToken;
        var scheduler = Substitute.For<IScheduler>();
        var tries = 0;
        var schedulers = Substitute.For<ISchedulerFactory>();
        schedulers.GetScheduler(Arg.Any<CancellationToken>()).Returns(_ => ++tries switch
        {
            1 or 3 or 5 => throw new SchedulerException(
                "The store failed.",
                new PostgresException("terminating connection due to administrator command", "FATAL", "FATAL", PostgresErrorCodes.AdminShutdown)),
            2 or 4 or 6 => throw new SchedulerException(
                "The store failed.",
                new PostgresException("cannot execute INSERT in a read-only transaction", "ERROR", "ERROR", PostgresErrorCodes.ReadOnlySqlTransaction)),
            _ => ValueTask.FromResult(scheduler),
        });
        var logs = new CapturingLoggerProvider();
        using var service = HostedServiceOver(new(fixture.Provider, fixture.ConnectionString), schedulers, logs);

        // Act
        await service.StartAsync(ct);
        var started = await JobsTestWait.UntilAsync(
            () => Task.FromResult(scheduler.ReceivedCalls().Any(call => call.GetMethodInfo().Name == nameof(IScheduler.Start))),
            TimeSpan.FromSeconds(60),
            ct);
        await service.StopAsync(ct);

        // Assert — each kind said once, not on each of its three tries.
        started.Should().BeTrue();
        logs.Entries.Count(entry => entry.Level == LogLevel.Error && entry.Message.StartsWith(UnreachableStartError, StringComparison.Ordinal))
            .Should().Be(1);
        logs.Entries.Count(entry => entry.Level == LogLevel.Error && entry.Message.StartsWith(ReadOnlyStartError, StringComparison.Ordinal))
            .Should().Be(1);
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
