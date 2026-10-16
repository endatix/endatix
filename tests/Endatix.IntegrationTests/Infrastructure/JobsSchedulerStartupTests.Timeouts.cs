using System.Net.Sockets;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Microsoft.Extensions.Logging;
using Npgsql;
using Quartz;

namespace Endatix.IntegrationTests;

/// <summary>A start whose scheduler build times out after the probe connected: a lock wait, or an outage after all.</summary>
public sealed partial class JobsSchedulerStartupTests
{
    private const string LostRaceWarning = "lost a race";

    [Fact]
    public async Task Scheduler_whose_build_times_out_on_a_broken_socket_waits_as_an_outage()
    {
        // Arrange — the probe connected, then a connection Quartz opened timed out in a network blip: the driver's
        // failure carries the broken socket under its timeout, and the network is back by the time anyone asks again.
        var ct = TestContext.Current.CancellationToken;
        var schedulers = new SchedulerFactoryFailingOnce(() => Task.FromResult<Exception>(new SchedulerException(
            "The store failed.",
            new NpgsqlException(
                "Exception while connecting",
                new TimeoutException("The operation has timed out.", new SocketException((int)SocketError.TimedOut))))));
        var logs = new CapturingLoggerProvider();
        using var service = HostedServiceOver(new(fixture.Provider, fixture.ConnectionString), schedulers, logs);

        // Act
        await service.StartAsync(ct);
        await service.StopAsync(ct);

        // Assert — reported and paced as an outage, not as a lock wait.
        logs.Entries.Should().Contain((LogLevel.Error, DeferredStartError));
        logs.Entries.Should().NotContain(entry => entry.Message.Contains(LostRaceWarning));
    }

    [Fact]
    public async Task Scheduler_whose_build_times_out_as_the_database_goes_away_waits_as_an_outage()
    {
        // Arrange — the probe connected, then the database is dropped while the build waits, so the wait runs out:
        // asking the probe again finds the database gone, which makes this an outage, not a lock wait.
        var ct = TestContext.Current.CancellationToken;
        var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        await using var dropped = database;
        var schedulers = new SchedulerFactoryFailingOnce(async () =>
        {
            await database.DisposeAsync();
            return new SchedulerException("The store failed.", new TimeoutException("The lock was not granted in time."));
        });
        var logs = new CapturingLoggerProvider();
        using var service = HostedServiceOver(new(database.Provider, database.ConnectionString), schedulers, logs);

        // Act
        await service.StartAsync(ct);
        await service.StopAsync(ct);

        // Assert
        logs.Entries.Should().Contain((LogLevel.Error, DeferredStartError));
        logs.Entries.Should().Contain(entry => entry.Level == LogLevel.Error && entry.Message.Contains("cannot open database"));
        logs.Entries.Should().NotContain(entry => entry.Message.Contains(LostRaceWarning));
    }

    // Fails the first build with what the test supplies, then builds a scheduler that does nothing.
    private sealed class SchedulerFactoryFailingOnce(Func<Task<Exception>> failure) : ISchedulerFactory
    {
        private readonly IScheduler _scheduler = Substitute.For<IScheduler>();
        private int _builds;

        public async ValueTask<IScheduler> GetScheduler(CancellationToken cancellationToken = default) =>
            Interlocked.Increment(ref _builds) == 1 ? throw await failure() : _scheduler;

        public ValueTask<List<IScheduler>> GetAllSchedulers(CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<List<IScheduler>>([_scheduler]);

        public ValueTask<IScheduler?> LookupScheduler(string schedulerName, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult<IScheduler?>(_scheduler);
    }
}
