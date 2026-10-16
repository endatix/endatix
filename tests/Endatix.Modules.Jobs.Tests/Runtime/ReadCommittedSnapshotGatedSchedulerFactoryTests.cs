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
        using var factory = Factory();
        var firstBuild = () => factory.GetScheduler().AsTask();
        await firstBuild.Should().ThrowAsync<SchedulerException>();

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
        using var factory = Factory();
        var firstBuild = () => factory.GetScheduler().AsTask();
        await firstBuild.Should().ThrowAsync<SchedulerException>();

        // Act
        await factory.GetScheduler();

        // Assert — no job types: the one thread a pool keeps, and the retention thread.
        using var rebuiltContainer = new ServiceCollection().BuildServiceProvider();
        _sizing.ThreadCount(rebuiltContainer).Should().Be(2);
    }

    [Fact]
    public async Task GetScheduler_AfterASuccessfulBuild_NeverRestarts()
    {
        // Arrange — built once, then shut down by the host stopping: Quartz's refusal stands.
        var built = Substitute.For<IScheduler>();
        _shipped.GetScheduler(Arg.Any<CancellationToken>()).Returns(
            _ => ValueTask.FromResult(built),
            _ => throw new SchedulerException("Scheduler 'endatix-jobs' has been shut down."));
        using var factory = Factory();
        await factory.GetScheduler();

        // Act
        var afterShutdown = () => factory.GetScheduler().AsTask();

        // Assert
        await afterShutdown.Should().ThrowAsync<SchedulerException>();
        await afterShutdown.Should().ThrowAsync<SchedulerException>();
        await _runtime.DidNotReceiveWithAnyArgs().Restart(default!, default!, default);
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
