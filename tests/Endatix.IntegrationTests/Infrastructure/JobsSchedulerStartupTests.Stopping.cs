using Endatix.IntegrationTests.Infrastructure.Jobs;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Quartz;

namespace Endatix.IntegrationTests;

/// <summary>A start still waiting for its database when the host stops, which Quartz refuses to rebuild for.</summary>
public sealed partial class JobsSchedulerStartupTests
{
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
        var logs = await StopWhileWaitingAsync(stopping => throw new SchedulerConfigException(HostStopping), stopBetweenTries: true, ct);

        // Assert
        logs.Entries.Should().NotContain(entry => entry.Level == LogLevel.Critical);
    }

    [Fact]
    public async Task Scheduler_whose_try_fails_because_the_host_is_stopping_stops_quietly()
    {
        // Arrange — the host starts stopping while a try is under way, which Quartz then refuses.
        var ct = TestContext.Current.CancellationToken;

        // Act
        var logs = await StopWhileWaitingAsync(
            stopping =>
            {
                stopping.Cancel();
                throw new SchedulerConfigException(HostStopping);
            },
            stopBetweenTries: false,
            ct);

        // Assert
        logs.Entries.Should().NotContain(entry => entry.Level == LogLevel.Critical);
    }

    // Starts the hosted service over a build that fails as in a failover, so the start waits; the next try runs
    // nextTry, which is given the host's stopping signal. Waits past that try, then stops.
    private async Task<CapturingLoggerProvider> StopWhileWaitingAsync(
        Func<CancellationTokenSource, IScheduler> nextTry,
        bool stopBetweenTries,
        CancellationToken ct)
    {
        using var stopping = new CancellationTokenSource();
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(stopping.Token);
        var schedulers = Substitute.For<ISchedulerFactory>();
        schedulers.GetScheduler(Arg.Any<CancellationToken>()).Returns<IScheduler>(
            _ => throw new SchedulerException(
                "The store failed.",
                new PostgresException("terminating connection due to administrator command", "FATAL", "FATAL", PostgresErrorCodes.AdminShutdown)),
            _ => nextTry(stopping));
        var logs = new CapturingLoggerProvider();
        using var service = HostedServiceOver(new(fixture.Provider, fixture.ConnectionString), schedulers, logs, lifetime);

        await service.StartAsync(ct);
        if (stopBetweenTries)
        {
            await stopping.CancelAsync();
        }

        await Task.Delay(TimeSpan.FromSeconds(7), ct);
        await service.StopAsync(ct);
        return logs;
    }
}
