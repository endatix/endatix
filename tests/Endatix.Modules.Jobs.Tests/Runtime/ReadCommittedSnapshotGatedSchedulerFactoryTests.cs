using Endatix.Modules.Jobs.Runtime;
using Endatix.Modules.Jobs.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Quartz;

namespace Endatix.Modules.Jobs.Tests.Runtime;

/// <summary>
/// The jobs scheduler's factory after a build that failed: whichever caller asks next, it builds another scheduler
/// rather than asking Quartz for the one it shut down.
/// </summary>
public sealed class ReadCommittedSnapshotGatedSchedulerFactoryTests : IDisposable
{
    private readonly ISchedulerFactory _shipped = Substitute.For<ISchedulerFactory>();
    private readonly RestartingRuntime _runtime = new();
    private readonly IScheduler _restarted = Substitute.For<IScheduler>();
    private readonly JobsSchedulerSizing _sizing = new();
    private readonly CancellationTokenSource _stopping = new();
    private readonly ILogger<ReadCommittedSnapshotGatedSchedulerFactory> _logger =
        Substitute.For<ILogger<ReadCommittedSnapshotGatedSchedulerFactory>>();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Long enough for any caller to finish; one left waiting on a rebuild nobody completes fails rather than hangs.
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    public void Dispose() => _stopping.Dispose();

    [Fact]
    public async Task GetScheduler_AfterAFailedFirstBuild_RestartsTheSchedulerWithoutStartingIt()
    {
        // Arrange — the first build failed, and Quartz now refuses the scheduler it shut down.
        _shipped.GetScheduler(Arg.Any<CancellationToken>()).Returns<IScheduler>(
            _ => throw new SchedulerException("The store failed."),
            _ => throw new SchedulerException("Scheduler 'endatix-jobs' has been shut down."),
            _ => _restarted);
        _runtime.OnRestart = _ => ValueTask.FromResult(_restarted);
        var factory = Factory();
        await FirstBuildFailsAsync(factory);

        // Act
        await factory.GetScheduler(Ct);

        // Assert
        _runtime.Restarts.Should().ContainSingle()
            .Which.Should().Match<RestartCall>(call => call.SchedulerName == QuartzRegistration.SchedulerName && call.Options.Start == false);
    }

    [Fact]
    public async Task GetScheduler_Rebuilding_WorksOutTheSizeFromTheApplicationFirst()
    {
        // Arrange — the rebuilt scheduler's container cannot resolve the job handlers its size comes from.
        _shipped.GetScheduler(Arg.Any<CancellationToken>()).Returns<IScheduler>(_ => throw new SchedulerException("The store failed."));
        _runtime.OnRestart = _ => ValueTask.FromResult(_restarted);
        var factory = Factory();
        await FirstBuildFailsAsync(factory);

        // Act
        await factory.GetScheduler(Ct);

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
        var rebuilds = new Queue<Task<IScheduler>>([failing.Task, succeeding.Task]);
        _runtime.OnRestart = _ => new ValueTask<IScheduler>(rebuilds.Dequeue());
        var factory = Factory();
        await FirstBuildFailsAsync(factory);

        // Act — five callers while the first rebuild runs, then five while the second does.
        var duringFirst = await CallersWaitingOnRestartAsync(factory, restarts: 1);
        failing.SetException(new SchedulerException("The store failed again."));
        var firstOutcomes = await Task.WhenAll(duringFirst.Select(OutcomeOf)).WaitAsync(Patience, Ct);
        var duringSecond = await CallersWaitingOnRestartAsync(factory, restarts: 2);
        succeeding.SetResult(_restarted);
        var schedulers = await Task.WhenAll(duringSecond).WaitAsync(Patience, Ct);

        // Assert
        firstOutcomes.Should().AllSatisfy(outcome => outcome.Should().BeOfType<SchedulerException>());
        schedulers.Should().AllSatisfy(scheduler => scheduler.Should().BeSameAs(_restarted));
        Restarts().Should().Be(2);
    }

    [Fact]
    public async Task GetScheduler_CallerCancelledDuringASharedRebuild_LeavesTheRebuildToTheOthers()
    {
        // Arrange
        var rebuilding = new TaskCompletionSource<IScheduler>(TaskCreationOptions.RunContinuationsAsynchronously);
        _shipped.GetScheduler(Arg.Any<CancellationToken>()).Returns<IScheduler>(_ => throw new SchedulerException("The store failed."));
        _runtime.OnRestart = _ => new ValueTask<IScheduler>(rebuilding.Task);
        var factory = Factory();
        await FirstBuildFailsAsync(factory);
        using var cancelled = new CancellationTokenSource();
        var leaving = SchedulerOfAsync(factory, cancelled.Token);
        var staying = SchedulerOfAsync(factory, Ct);
        await RestartsAsync(1);

        // Act
        await cancelled.CancelAsync();
        var left = () => leaving.WaitAsync(Patience, Ct);
        await left.Should().ThrowAsync<OperationCanceledException>();
        rebuilding.SetResult(_restarted);

        // Assert — the rebuild ran on no caller's token, and finished for the caller still waiting.
        (await staying.WaitAsync(Patience, Ct)).Should().BeSameAs(_restarted);
        RestartToken().IsCancellationRequested.Should().BeFalse();
    }

    [Fact]
    public async Task GetScheduler_HostStoppingDuringASharedRebuild_CancelsTheRebuild()
    {
        // Arrange
        var rebuilding = new TaskCompletionSource<IScheduler>(TaskCreationOptions.RunContinuationsAsynchronously);
        _shipped.GetScheduler(Arg.Any<CancellationToken>()).Returns<IScheduler>(_ => throw new SchedulerException("The store failed."));
        _runtime.OnRestart = _ => new ValueTask<IScheduler>(rebuilding.Task);
        var factory = Factory();
        await FirstBuildFailsAsync(factory);
        _ = SchedulerOfAsync(factory, Ct);
        await RestartsAsync(1);

        // Act
        await _stopping.CancelAsync();

        // Assert — the build does not outlive the host.
        RestartToken().IsCancellationRequested.Should().BeTrue();
    }

    [Fact]
    public async Task GetScheduler_RebuildRefusedWhileTheHostStops_FailsWithACancellation()
    {
        // Arrange — Quartz refuses to rebuild once the host is stopping; the caller's own token is not cancelled.
        _shipped.GetScheduler(Arg.Any<CancellationToken>()).Returns<IScheduler>(_ => throw new SchedulerException("The store failed."));
        _runtime.OnRestart = _ =>
        {
            _stopping.Cancel();
            throw new SchedulerConfigException("Quartz will not rebuild a scheduler while the host stops.");
        };
        var factory = Factory();
        await FirstBuildFailsAsync(factory);

        // Act
        var rebuild = () => factory.GetScheduler(Ct).AsTask();

        // Assert — a cancellation, told apart by its type, carrying what Quartz said, which is left at Debug.
        (await rebuild.Should().ThrowAsync<OperationCanceledException>()).Which.InnerException.Should().BeOfType<SchedulerConfigException>();
        (await LoggedAtDebugAsync(exception => exception is SchedulerConfigException)).Should().BeTrue();
    }

    [Fact]
    public async Task GetScheduler_RebuildFailingAfterEveryCallerLeft_LogsTheFailure()
    {
        // Arrange — the only caller stops waiting before the rebuild fails.
        var rebuilding = new TaskCompletionSource<IScheduler>(TaskCreationOptions.RunContinuationsAsynchronously);
        _shipped.GetScheduler(Arg.Any<CancellationToken>()).Returns<IScheduler>(_ => throw new SchedulerException("The store failed."));
        _runtime.OnRestart = _ => new ValueTask<IScheduler>(rebuilding.Task);
        var factory = Factory();
        await FirstBuildFailsAsync(factory);
        using var cancelled = new CancellationTokenSource();
        var leaving = SchedulerOfAsync(factory, cancelled.Token);
        await RestartsAsync(1);
        await cancelled.CancelAsync();
        await OutcomeOf(leaving);

        // Act
        rebuilding.SetException(new SchedulerException("The store failed again."));
        var logged = await LoggedAtDebugAsync();

        // Assert
        logged.Should().BeTrue();
    }

    [Fact]
    public async Task ListingAndLookup_WhileTheBuildFails_NeitherBuildNorThrow()
    {
        // Arrange — the first build failed and left Quartz holding a scheduler it shut down; another scheduler is up.
        var shutDown = Scheduler(QuartzRegistration.SchedulerName);
        var other = Scheduler("other");
        _shipped.GetScheduler(Arg.Any<CancellationToken>()).Returns<IScheduler>(_ => throw new SchedulerException("The store failed."));
        _shipped.GetAllSchedulers(Arg.Any<CancellationToken>()).Returns([shutDown, other]);
        var factory = Factory();
        await FirstBuildFailsAsync(factory);

        // Act
        var listed = await factory.GetAllSchedulers(Ct);
        var lookedUp = await factory.LookupScheduler("ENDATIX-JOBS", Ct);

        // Assert — no build, no rebuild, no failure, and nothing handed out of the failed build.
        listed.Should().Equal(other);
        lookedUp.Should().BeNull();
        await _shipped.Received(1).GetScheduler(Arg.Any<CancellationToken>());
        await _shipped.DidNotReceive().LookupScheduler(Arg.Any<string>(), Arg.Any<CancellationToken>());
        Restarts().Should().Be(0);
    }

    [Fact]
    public async Task LookupScheduler_AfterASuccessfulBuild_FindsTheScheduler()
    {
        // Arrange
        var built = Scheduler(QuartzRegistration.SchedulerName);
        _shipped.GetScheduler(Arg.Any<CancellationToken>()).Returns(built);
        _shipped.LookupScheduler(QuartzRegistration.SchedulerName, Arg.Any<CancellationToken>()).Returns<IScheduler?>(built);
        var factory = Factory();
        await factory.GetScheduler(Ct);

        // Act
        var lookedUp = await factory.LookupScheduler(QuartzRegistration.SchedulerName, Ct);

        // Assert
        lookedUp.Should().BeSameAs(built);
    }

    [Fact]
    public void Dispose_Twice_DoesNotThrow()
    {
        // Arrange — the container disposes the factory, and a test or host may dispose it again.
        var factory = Factory();
        factory.Dispose();

        // Act
        var disposeAgain = factory.Dispose;

        // Assert
        disposeAgain.Should().NotThrow();
    }

    [Fact]
    public async Task Dispose_DuringASharedRebuild_CancelsItReleasesItsTokenAndRefusesNewBuilds()
    {
        // Arrange
        var rebuilding = new TaskCompletionSource<IScheduler>(TaskCreationOptions.RunContinuationsAsynchronously);
        _shipped.GetScheduler(Arg.Any<CancellationToken>()).Returns<IScheduler>(_ => throw new SchedulerException("The store failed."));
        _runtime.OnRestart = _ => new ValueTask<IScheduler>(rebuilding.Task);
        var factory = Factory();
        await FirstBuildFailsAsync(factory);
        _ = SchedulerOfAsync(factory, Ct);
        await RestartsAsync(1);

        // Act
        factory.Dispose();
        var buildAfterwards = () => factory.GetScheduler(Ct).AsTask();

        // Assert — the build was told to stop, no token source is left to leak, and none is made after.
        RestartToken().IsCancellationRequested.Should().BeTrue();
        BuildingTokenSource(factory).Should().BeNull();
        await buildAfterwards.Should().ThrowAsync<ObjectDisposedException>();
        BuildingTokenSource(factory).Should().BeNull();
    }

    [Fact]
    public async Task GetScheduler_AfterASuccessfulBuild_NeverRestarts()
    {
        // Arrange — built once, then shut down by the host stopping: Quartz's refusal stands.
        var built = Substitute.For<IScheduler>();
        _shipped.GetScheduler(Arg.Any<CancellationToken>()).Returns<IScheduler>(
            _ => built,
            _ => throw new SchedulerException("Scheduler 'endatix-jobs' has been shut down."));
        var factory = Factory();
        await factory.GetScheduler(Ct);

        // Act
        var afterShutdown = () => factory.GetScheduler(Ct).AsTask();

        // Assert
        await afterShutdown.Should().ThrowAsync<SchedulerException>();
        await afterShutdown.Should().ThrowAsync<SchedulerException>();
        Restarts().Should().Be(0);
    }

    private static async Task<IScheduler> SchedulerOfAsync(ReadCommittedSnapshotGatedSchedulerFactory factory, CancellationToken cancellationToken) =>
        await factory.GetScheduler(cancellationToken);

    private static async Task FirstBuildFailsAsync(ReadCommittedSnapshotGatedSchedulerFactory factory)
    {
        var failure = default(Exception);
        try
        {
            await factory.GetScheduler(Ct);
        }
        catch (SchedulerException exception)
        {
            failure = exception;
        }

        failure.Should().NotBeNull("the first build fails");
    }

    // Starts five callers and waits until the rebuild they share has asked for its restart.
    private async Task<Task<IScheduler>[]> CallersWaitingOnRestartAsync(ReadCommittedSnapshotGatedSchedulerFactory factory, int restarts)
    {
        var callers = Enumerable.Range(0, 5).Select(_ => factory.GetScheduler(Ct).AsTask()).ToArray();
        await RestartsAsync(restarts);
        return callers;
    }

    private async Task RestartsAsync(int count)
    {
        for (var waited = 0; Restarts() < count && waited < 200; waited++)
        {
            await Task.Delay(10, Ct);
        }

        // Every caller has had its turn to start a build of its own, had it been allowed one.
        await Task.Delay(50, Ct);
    }

    // The factory's own token source, read where it is kept: whether one is left over is not visible otherwise.
    private static object? BuildingTokenSource(ReadCommittedSnapshotGatedSchedulerFactory factory) =>
        typeof(ReadCommittedSnapshotGatedSchedulerFactory)
            .GetField("_building", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(factory);

    private static IScheduler Scheduler(string name)
    {
        var scheduler = Substitute.For<IScheduler>();
        scheduler.SchedulerName.Returns(name);
        return scheduler;
    }

    private CancellationToken RestartToken() => _runtime.Restarts.Single().CancellationToken;

    private async Task<bool> LoggedAtDebugAsync(Func<Exception?, bool>? withException = null)
    {
        for (var waited = 0; waited < 200; waited++)
        {
            if (_logger.ReceivedCalls().Any(call => call.GetMethodInfo().Name == nameof(ILogger.Log)
                && (LogLevel)call.GetArguments()[0]! == LogLevel.Debug
                && (withException?.Invoke(call.GetArguments()[3] as Exception) ?? true)))
            {
                return true;
            }

            await Task.Delay(10, Ct);
        }

        return false;
    }

    private int Restarts() => _runtime.Restarts.Count;

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
            .AddSingleton<ISchedulerRuntime>(_runtime)
            .AddSingleton(Lifetime())
            .AddSingleton(_logger)
            .AddSingleton(_sizing)
            .AddSingleton(JobHandlerRegistry.Build([]))
            .AddSingleton(new JobTypePolicies(Options.Create(new BackgroundJobsOptions()), DeclaredDefaults.None))
            .BuildServiceProvider();
        var database = new JobStoreDatabase("Host=localhost", isPostgreSql: true, locksOnInsert: false);
        return new ReadCommittedSnapshotGatedSchedulerFactory(_shipped, services, new ReadCommittedSnapshotRequirement(database));
    }

    /// <param name="SchedulerName">The scheduler asked for.</param>
    /// <param name="Options">How it was to be rebuilt.</param>
    /// <param name="CancellationToken">The token it was rebuilt on.</param>
    private sealed record RestartCall(string SchedulerName, SchedulerRestartOptions Options, CancellationToken CancellationToken);

    // Quartz's runtime as far as a rebuild goes: records each Restart and answers it as the test says. A substitute
    // would be configured by calling Restart, whose ValueTask the configuration then holds on to (CA2012).
    private sealed class RestartingRuntime : ISchedulerRuntime
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<RestartCall> _restarts = new();

        public Func<CancellationToken, ValueTask<IScheduler>> OnRestart { get; set; } =
            _ => throw new InvalidOperationException("The test did not expect a rebuild.");

        public System.Collections.Concurrent.ConcurrentQueue<RestartCall> Restarts => _restarts;

        public ValueTask<IScheduler> Restart(
            string schedulerName,
            SchedulerRestartOptions options = default,
            CancellationToken cancellationToken = default)
        {
            _restarts.Enqueue(new RestartCall(schedulerName, options, cancellationToken));
            return OnRestart(cancellationToken);
        }

        public ValueTask<IScheduler> Add(
            string schedulerName,
            Action<IQuartzBuilder>? configure = null,
            SchedulerAddOptions options = default,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<bool> Remove(string schedulerName, bool waitForJobsToComplete = false, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public ValueTask<List<SchedulerRegistration>> QuerySchedulers(CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();
    }

    private IHostApplicationLifetime Lifetime()
    {
        var lifetime = Substitute.For<IHostApplicationLifetime>();
        lifetime.ApplicationStopping.Returns(_stopping.Token);
        return lifetime;
    }
}
