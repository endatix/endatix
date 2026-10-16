using Endatix.Modules.Jobs.Runtime;
using Endatix.Modules.Jobs.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Quartz;

namespace Endatix.Modules.Jobs.Tests.Runtime;

/// <summary>
/// The jobs scheduler's factory after a build that failed: whichever caller asks next, it builds another scheduler
/// rather than asking Quartz for the one it shut down.
/// </summary>
public sealed class ReadCommittedSnapshotGatedSchedulerFactoryTests
{
    private readonly ISchedulerFactory _shipped = Substitute.For<ISchedulerFactory>();
    private readonly ISchedulerRuntime _runtime = Substitute.For<ISchedulerRuntime>();
    private readonly IScheduler _restarted = Substitute.For<IScheduler>();
    private readonly JobsSchedulerSizing _sizing = new();

    // Long enough for any caller to finish; one left waiting on a rebuild nobody completes fails rather than hangs.
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    public static TheoryData<string> Callers() => [nameof(ISchedulerFactory.GetScheduler), nameof(ISchedulerFactory.LookupScheduler), nameof(ISchedulerFactory.GetAllSchedulers)];

    [Theory]
    [MemberData(nameof(Callers))]
    public async Task Caller_AfterAFailedFirstBuild_RestartsTheSchedulerWithoutStartingIt(string caller)
    {
        // Arrange — the first build failed, and Quartz now refuses the scheduler it shut down.
        _shipped.GetScheduler(Arg.Any<CancellationToken>()).Returns(
            _ => throw new SchedulerException("The store failed."),
            _ => throw new SchedulerException("Scheduler 'endatix-jobs' has been shut down."),
            _ => ValueTask.FromResult(_restarted));
        _runtime.Restart(QuartzRegistration.SchedulerName, Arg.Any<SchedulerRestartOptions>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(_restarted));
        var factory = Factory();
        await FirstBuildFailsAsync(factory);

        // Act
        await AskAsync(factory, caller);

        // Assert
        await _runtime.Received(1).Restart(
            QuartzRegistration.SchedulerName,
            Arg.Is<SchedulerRestartOptions>(options => options.Start == false),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetScheduler_Rebuilding_WorksOutTheSizeFromTheApplicationFirst()
    {
        // Arrange — the rebuilt scheduler's container cannot resolve the job handlers its size comes from.
        _shipped.GetScheduler(Arg.Any<CancellationToken>()).Returns<IScheduler>(_ => throw new SchedulerException("The store failed."));
        _runtime.Restart(QuartzRegistration.SchedulerName, Arg.Any<SchedulerRestartOptions>(), Arg.Any<CancellationToken>())
            .Returns(ValueTask.FromResult(_restarted));
        var factory = Factory();
        await FirstBuildFailsAsync(factory);

        // Act
        await factory.GetScheduler();

        // Assert — no job types: the one thread a pool keeps, and the retention thread.
        using var rebuiltContainer = new ServiceCollection().BuildServiceProvider();
        _sizing.ThreadCount(rebuiltContainer).Should().Be(2);
    }

    [Fact]
    public async Task GetScheduler_ManyCallersDuringAFailingRebuild_ShareOneRestartPerAttempt()
    {
        // Arrange — the first build failed; the first rebuild fails too, and the second succeeds.
        var failing = new TaskCompletionSource<IScheduler>(TaskCreationOptions.RunContinuationsAsynchronously);
        var succeeding = new TaskCompletionSource<IScheduler>(TaskCreationOptions.RunContinuationsAsynchronously);
        _shipped.GetScheduler(Arg.Any<CancellationToken>()).Returns<IScheduler>(_ => throw new SchedulerException("The store failed."));
        _runtime.Restart(QuartzRegistration.SchedulerName, Arg.Any<SchedulerRestartOptions>(), Arg.Any<CancellationToken>())
            .Returns(new ValueTask<IScheduler>(failing.Task), new ValueTask<IScheduler>(succeeding.Task));
        var factory = Factory();
        await FirstBuildFailsAsync(factory);

        // Act — five callers while the first rebuild runs, then five while the second does.
        var duringFirst = await CallersWaitingOnRestartAsync(factory, restarts: 1);
        failing.SetException(new SchedulerException("The store failed again."));
        var firstOutcomes = await Task.WhenAll(duringFirst.Select(OutcomeOf)).WaitAsync(Patience);
        var duringSecond = await CallersWaitingOnRestartAsync(factory, restarts: 2);
        succeeding.SetResult(_restarted);
        var schedulers = await Task.WhenAll(duringSecond).WaitAsync(Patience);

        // Assert
        firstOutcomes.Should().AllSatisfy(outcome => outcome.Should().BeOfType<SchedulerException>());
        schedulers.Should().AllSatisfy(scheduler => scheduler.Should().BeSameAs(_restarted));
        await _runtime.Received(2).Restart(
            QuartzRegistration.SchedulerName, Arg.Any<SchedulerRestartOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetScheduler_CallerCancelledDuringASharedRebuild_LeavesTheRebuildToTheOthers()
    {
        // Arrange
        var rebuilding = new TaskCompletionSource<IScheduler>(TaskCreationOptions.RunContinuationsAsynchronously);
        _shipped.GetScheduler(Arg.Any<CancellationToken>()).Returns<IScheduler>(_ => throw new SchedulerException("The store failed."));
        _runtime.Restart(QuartzRegistration.SchedulerName, Arg.Any<SchedulerRestartOptions>(), Arg.Any<CancellationToken>())
            .Returns(_ => new ValueTask<IScheduler>(rebuilding.Task));
        var factory = Factory();
        await FirstBuildFailsAsync(factory);
        using var cancelled = new CancellationTokenSource();
        var leaving = SchedulerOfAsync(factory, cancelled.Token);
        var staying = SchedulerOfAsync(factory, CancellationToken.None);
        await RestartsAsync(1);

        // Act
        await cancelled.CancelAsync();
        var left = () => leaving.WaitAsync(Patience);
        await left.Should().ThrowAsync<OperationCanceledException>();
        rebuilding.SetResult(_restarted);

        // Assert — the rebuild ran on no caller's token, and finished for the caller still waiting.
        (await staying.WaitAsync(Patience)).Should().BeSameAs(_restarted);
        await _runtime.Received(1).Restart(
            QuartzRegistration.SchedulerName,
            Arg.Any<SchedulerRestartOptions>(),
            Arg.Is<CancellationToken>(token => !token.CanBeCanceled));
    }

    [Fact]
    public async Task GetScheduler_AfterASuccessfulBuild_NeverRestarts()
    {
        // Arrange — built once, then shut down by the host stopping: Quartz's refusal stands.
        var built = Substitute.For<IScheduler>();
        _shipped.GetScheduler(Arg.Any<CancellationToken>()).Returns(
            _ => ValueTask.FromResult(built),
            _ => throw new SchedulerException("Scheduler 'endatix-jobs' has been shut down."));
        var factory = Factory();
        await factory.GetScheduler();

        // Act
        var afterShutdown = () => factory.GetScheduler().AsTask();

        // Assert
        await afterShutdown.Should().ThrowAsync<SchedulerException>();
        await afterShutdown.Should().ThrowAsync<SchedulerException>();
        await _runtime.DidNotReceiveWithAnyArgs().Restart(default!, default!, default);
    }

    private static async Task<IScheduler> SchedulerOfAsync(ISchedulerFactory factory, CancellationToken cancellationToken) =>
        await factory.GetScheduler(cancellationToken);

    private static async Task FirstBuildFailsAsync(ISchedulerFactory factory)
    {
        var failure = default(Exception);
        try
        {
            await factory.GetScheduler();
        }
        catch (SchedulerException exception)
        {
            failure = exception;
        }

        failure.Should().NotBeNull("the first build fails");
    }

    // Starts five callers and waits until the rebuild they share has asked for its restart.
    private async Task<Task<IScheduler>[]> CallersWaitingOnRestartAsync(ISchedulerFactory factory, int restarts)
    {
        var callers = Enumerable.Range(0, 5).Select(_ => factory.GetScheduler().AsTask()).ToArray();
        await RestartsAsync(restarts);
        return callers;
    }

    private async Task RestartsAsync(int count)
    {
        for (var waited = 0; Restarts() < count && waited < 200; waited++)
        {
            await Task.Delay(10);
        }

        // Every caller has had its turn to start a build of its own, had it been allowed one.
        await Task.Delay(50);
    }

    private int Restarts() =>
        _runtime.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(ISchedulerRuntime.Restart));

    private static async Task<Exception?> OutcomeOf(Task<IScheduler> caller)
    {
        try
        {
            await caller;
            return null;
        }
        catch (Exception exception)
        {
            return exception;
        }
    }

    private ReadCommittedSnapshotGatedSchedulerFactory Factory()
    {
        var services = new ServiceCollection()
            .AddSingleton(_runtime)
            .AddSingleton(_sizing)
            .AddSingleton(JobHandlerRegistry.Build([]))
            .AddSingleton(new JobTypePolicies(Options.Create(new BackgroundJobsOptions()), DeclaredDefaults.None))
            .BuildServiceProvider();
        var database = new JobStoreDatabase("Host=localhost", isPostgreSql: true, locksOnInsert: false);
        return new ReadCommittedSnapshotGatedSchedulerFactory(_shipped, services, new ReadCommittedSnapshotRequirement(database));
    }

    private static async Task AskAsync(ISchedulerFactory factory, string caller)
    {
        switch (caller)
        {
            case nameof(ISchedulerFactory.GetScheduler):
                await factory.GetScheduler();
                break;
            case nameof(ISchedulerFactory.LookupScheduler):
                await factory.LookupScheduler(QuartzRegistration.SchedulerName);
                break;
            default:
                await factory.GetAllSchedulers();
                break;
        }
    }
}
