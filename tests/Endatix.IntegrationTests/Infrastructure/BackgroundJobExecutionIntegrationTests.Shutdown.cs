using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Endatix.IntegrationTests;

/// <summary>
/// A stopping node tells the handlers still running to stop once its scheduler has let go of them, however the
/// scheduler's shutdown ended.
/// </summary>
public sealed partial class BackgroundJobExecutionIntegrationTests
{
    [Fact]
    public async Task Stopping_node_whose_wait_for_running_jobs_runs_out_tells_their_handlers_to_stop()
    {
        // Arrange — the handler waits on its token, which only the shutdown signal cancels, so the wait runs out.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        var invocations = new ProbeInvocations();
        await using var node = await StartNodeAsync(new(database, invocations, StoppingQuickly("node-wait")), ct);
        var jobId = await node.EnqueueAsync(Probe(ProbeBehaviours.Block), ct);
        await JobsTestWait.UntilAsync(() => Task.FromResult(invocations.CountFor(jobId) == 1), Patience, ct);

        // Act
        var stop = () => node.StopAsync(ct);

        // Assert — the scheduler gave up waiting without throwing, and the handler was then told to stop.
        await stop.Should().NotThrowAsync();
        (await JobsTestWait.UntilAsync(
            () => Task.FromResult(invocations.CancelledAt(jobId) is not null), TimeSpan.FromSeconds(5), ct)).Should().BeTrue();
    }

    [Fact]
    public async Task Stopping_node_whose_scheduler_fails_to_shut_down_still_raises_the_shutdown_signal()
    {
        // Arrange — the scheduler's teardown can fail after it has let go of the running jobs, as a job store
        // that cannot close does.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        await using var node = JobsTestNode.Create(database, configureServices: UseSchedulerThatFailsToShutDown);
        await node.StartAsync(ct);

        // Act
        var stop = () => node.StopAsync(ct);

        // Assert
        await stop.Should().ThrowAsync<SchedulerException>();
        node.Services.GetRequiredService<JobsShutdownSignal>().IsRaised.Should().BeTrue();
    }

    // Registered after the module, so the node resolves this factory in place of the one the module registered.
    private static void UseSchedulerThatFailsToShutDown(IServiceCollection services)
    {
        var scheduler = Substitute.For<IScheduler>();
        scheduler
            .Shutdown(Arg.Any<bool>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromException(new SchedulerException("The job store could not be closed.")));
        var factory = Substitute.For<ISchedulerFactory>();
        factory.GetScheduler(Arg.Any<CancellationToken>()).Returns(new ValueTask<IScheduler>(scheduler));
        services.AddKeyedSingleton(QuartzRegistration.SchedulerName, factory);
    }
}
