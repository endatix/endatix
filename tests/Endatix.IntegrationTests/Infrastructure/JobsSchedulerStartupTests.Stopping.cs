using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Quartz;

namespace Endatix.IntegrationTests;

/// <summary>A start still waiting for its database when the host stops, which Quartz refuses to rebuild for.</summary>
public sealed partial class JobsSchedulerStartupTests
{
    private const string StoppedBeforeStarting = "The background jobs scheduler could not start before the host stopped";

    // What Quartz 4.2.2 throws when asked to rebuild a scheduler once the host is stopping.
    private const string HostStopping =
        "The host is stopping, so scheduler 'endatix-jobs' was not restarted: it would be created after the shutdown that would have stopped it, and nothing would come back for it.";

    [Fact]
    public async Task Scheduler_waiting_when_the_host_starts_stopping_tries_no_more_and_stops_quietly()
    {
        // Arrange — the host starts stopping between two tries, and its other services take longer to stop than the
        // wait before the next try.
        var ct = TestContext.Current.CancellationToken;

        // Act
        var logs = await StopWhileWaitingAsync(
            stopping => throw new SchedulerConfigException(HostStopping), new(StopBetweenTries: true, ThroughTheModulesFactory: true), ct);

        // Assert
        logs.Entries.Should().NotContain(entry => entry.Level >= LogLevel.Warning && entry.Message.Contains(StoppedBeforeStarting));
        logs.Entries.Should().NotContain(entry => entry.Level == LogLevel.Critical);
    }

    [Fact]
    public async Task Scheduler_whose_try_fails_because_the_host_is_stopping_stops_quietly()
    {
        // Arrange — the host starts stopping while a rebuild is under way, which Quartz then refuses; the module's factory
        // turns that refusal into the cancellation the stop caused, whatever Quartz says.
        var ct = TestContext.Current.CancellationToken;

        // Act
        var logs = await StopWhileWaitingAsync(
            stopping =>
            {
                stopping.Cancel();
                throw new SchedulerConfigException(HostStopping);
            },
            new(StopBetweenTries: false, ThroughTheModulesFactory: true),
            ct);

        // Assert
        logs.Entries.Should().NotContain(entry => entry.Level >= LogLevel.Warning && entry.Message.Contains(StoppedBeforeStarting));
        logs.Entries.Should().NotContain(entry => entry.Level == LogLevel.Critical);
    }

    [Fact]
    public async Task Scheduler_whose_try_is_refused_as_the_host_starts_stopping_says_why_at_warning()
    {
        // Arrange — the host starts stopping while a try is under way, and the try is refused for a reason of its own,
        // which still says why this host ran no jobs.
        var ct = TestContext.Current.CancellationToken;
        const string refusal = "The background jobs scheduler cannot start: READ_COMMITTED_SNAPSHOT is off.";

        // Act
        var logs = await StopWhileWaitingAsync(
            stopping =>
            {
                stopping.Cancel();
                throw new InvalidOperationException(refusal);
            },
            new(StopBetweenTries: false, ThroughTheModulesFactory: false),
            ct);

        // Assert
        logs.Entries.Should().Contain(entry => entry.Level == LogLevel.Warning
            && entry.Message.StartsWith(StoppedBeforeStarting, StringComparison.Ordinal)
            && entry.Message.Contains(refusal));
        logs.Entries.Should().NotContain(entry => entry.Level == LogLevel.Critical);
    }

    // Starts the hosted service over a build that fails as in a failover, so the start waits; the next try runs
    // nextTry, which is given the host's stopping signal. Through the module's factory the next try is its rebuild, as
    // on a real node; otherwise the hosted service sees what nextTry throws as it is. Waits past that try, then stops.
    private async Task<CapturingLoggerProvider> StopWhileWaitingAsync(
        Func<CancellationTokenSource, IScheduler> nextTry,
        StopTiming timing,
        CancellationToken ct)
    {
        using var stopping = new CancellationTokenSource();
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(stopping.Token);
        var logs = new CapturingLoggerProvider();
        var schedulers = FailingOverThen(() => nextTry(stopping), lifetime, timing.ThroughTheModulesFactory);
        using var service = HostedServiceOver(
            new HostedServiceParts(new(fixture.Provider, fixture.ConnectionString), schedulers, logs) { Lifetime = lifetime });

        await service.StartAsync(ct);
        if (timing.StopBetweenTries)
        {
            await stopping.CancelAsync();
        }

        await Task.Delay(TimeSpan.FromSeconds(7), ct);
        await service.StopAsync(ct);
        (schedulers as IDisposable)?.Dispose();
        return logs;
    }

    // A first build that fails as in a failover, and a next try that runs nextTry: the module's factory rebuilding the
    // scheduler, or a bare factory building it again.
    private static ISchedulerFactory FailingOverThen(Func<IScheduler> nextTry, IHostApplicationLifetime lifetime, bool throughTheModulesFactory)
    {
        var shipped = Substitute.For<ISchedulerFactory>();
        var failover = new SchedulerException(
            "The store failed.",
            new PostgresException("terminating connection due to administrator command", "FATAL", "FATAL", PostgresErrorCodes.AdminShutdown));
        if (!throughTheModulesFactory)
        {
            shipped.GetScheduler(Arg.Any<CancellationToken>()).Returns<IScheduler>(_ => throw failover, _ => nextTry());
            return shipped;
        }

        shipped.GetScheduler(Arg.Any<CancellationToken>()).Returns<IScheduler>(_ => throw failover);
        var runtime = Substitute.For<ISchedulerRuntime>();
        runtime.Restart(Arg.Any<string>(), Arg.Any<SchedulerRestartOptions>(), Arg.Any<CancellationToken>())
            .Returns<IScheduler>(_ => nextTry());
        return ModulesFactoryOver(shipped, runtime, lifetime);
    }

    // The module's own factory, over Quartz's that a test controls, with the services a rebuild resolves.
    private static ReadCommittedSnapshotGatedSchedulerFactory ModulesFactoryOver(
        ISchedulerFactory shipped,
        ISchedulerRuntime runtime,
        IHostApplicationLifetime lifetime)
    {
        var services = new ServiceCollection()
            .AddSingleton(runtime)
            .AddSingleton(lifetime)
            .AddSingleton(new JobsSchedulerSizing())
            .AddSingleton(JobHandlerRegistry.Build([]))
            .AddSingleton(new JobTypePolicies(Options.Create(new BackgroundJobsOptions()), new JobTypeDefaults([])))
            .BuildServiceProvider();
        var database = new JobStoreDatabase("Host=localhost", isPostgreSql: true, locksOnInsert: false);
        return new ReadCommittedSnapshotGatedSchedulerFactory(shipped, services, new ReadCommittedSnapshotRequirement(database));
    }

    /// <param name="StopBetweenTries">The host starts stopping before the next try rather than during it.</param>
    /// <param name="ThroughTheModulesFactory">The next try is the module's factory rebuilding the scheduler.</param>
    private sealed record StopTiming(bool StopBetweenTries, bool ThroughTheModulesFactory);
}
