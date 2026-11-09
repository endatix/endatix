using System.Diagnostics;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.Modules.Jobs.Runtime;
using Npgsql;
using Quartz;

namespace Endatix.IntegrationTests;

/// <summary>How long a failed start waits before its next try, by kind of failure.</summary>
public sealed partial class JobsSchedulerStartupTests
{
    // Far apart, so a wait at one pace cannot pass for the other however slow a probe between tries is.
    private static readonly SchedulerStartRetryDelays DistinctRetryDelays =
        new(Unreachable: TimeSpan.FromSeconds(1), Contended: TimeSpan.FromMilliseconds(20));

    // A timer may fire a clock tick early.
    private static readonly TimeSpan TimerSlack = TimeSpan.FromMilliseconds(50);

    [Fact]
    public async Task Scheduler_tries_the_first_three_lost_races_again_sooner_than_a_fourth_or_an_outage()
    {
        // Arrange — four lost races in a row, then a failover, then a build that succeeds.
        var ct = TestContext.Current.CancellationToken;
        var scheduler = Substitute.For<IScheduler>();
        var tries = new List<long>();
        var schedulers = LosingFourRacesThenFailingOver(scheduler, tries);
        var logs = new CapturingLoggerProvider();
        using var service = HostedServiceOver(
            new HostedServiceParts(new(fixture.Provider, fixture.ConnectionString), schedulers, logs) { RetryDelays = DistinctRetryDelays });

        // Act
        await service.StartAsync(ct);
        var started = await JobsTestWait.UntilAsync(
            () => Task.FromResult(scheduler.ReceivedCalls().Any(call => call.GetMethodInfo().Name == nameof(IScheduler.Start))),
            TimeSpan.FromSeconds(20),
            ct);
        await service.StopAsync(ct);

        // Assert — the wait before each try: three at the lost-race pace, then the outage's pace after the fourth lost
        // race and after the failover.
        started.Should().BeTrue();
        var waits = tries.Zip(tries.Skip(1), Stopwatch.GetElapsedTime).ToList();
        waits.Should().HaveCount(5);
        waits.Take(3).Should().AllSatisfy(wait => wait.Should().BeLessThan(DistinctRetryDelays.Unreachable));
        waits.Skip(3).Should().AllSatisfy(wait => wait.Should().BeGreaterThanOrEqualTo(DistinctRetryDelays.Unreachable - TimerSlack));
    }

    // Records when each build is tried: four fail as lost races, the fifth as a failover, and the sixth succeeds.
    private static ISchedulerFactory LosingFourRacesThenFailingOver(IScheduler scheduler, List<long> tries)
    {
        var schedulers = Substitute.For<ISchedulerFactory>();
        schedulers.GetScheduler(Arg.Any<CancellationToken>()).Returns<IScheduler>(_ =>
        {
            tries.Add(Stopwatch.GetTimestamp());
            return tries.Count switch
            {
                <= 4 => throw new SchedulerException(
                    "The store failed.",
                    new PostgresException("deadlock detected", "ERROR", "ERROR", PostgresErrorCodes.DeadlockDetected)),
                5 => throw new SchedulerException(
                    "The store failed.",
                    new PostgresException("terminating connection due to administrator command", "FATAL", "FATAL", PostgresErrorCodes.AdminShutdown)),
                _ => scheduler,
            };
        });
        return schedulers;
    }
}
