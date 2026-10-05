using Endatix.Core.Abstractions;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using static Endatix.Modules.Jobs.Tests.Runtime.JobExecutionTestHost;

namespace Endatix.Modules.Jobs.Tests.Runtime;

/// <summary>Firings this node does not run: they leave the job row untouched.</summary>
public sealed partial class BackgroundJobExecutionTests
{
    [Fact]
    public async Task Execute_UnknownJobType_DeclinesAndReschedules()
    {
        // Arrange — this node has no handler for "Orphan", yet its trigger fired here.
        var now = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        await using var provider = Services(repository, new ObservedRun());
        provider.GetRequiredService<IDateTimeProvider>().UtcNow.Returns(now);
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = FiringOf(JobId, jobType: "Orphan");
        ITrigger? rescheduled = null;
        context.Scheduler
            .RescheduleJob(Arg.Any<TriggerKey>(), Arg.Do<ITrigger>(trigger => rescheduled = trigger), Arg.Any<CancellationToken>())
            .Returns(now.AddSeconds(30));

        // Act
        await execution.Execute(context, TestContext.Current.CancellationToken);

        // Assert — the row is never touched, and the same trigger fires again in thirty seconds.
        repository.ReceivedCalls().Should().BeEmpty();
        rescheduled.Should().NotBeNull();
        rescheduled.Key.Should().Be(context.Trigger.Key);
        rescheduled.StartTimeUtc.Should().BeCloseTo(now.AddSeconds(30), TimeSpan.FromSeconds(2));
        rescheduled.JobDataMap.GetString(BackgroundJobExecution.JobIdKey).Should().Be(JobId.ToString());
        rescheduled.ExecutionGroup.Should().Be("Orphan");
    }

    [Fact]
    public async Task Execute_UnknownJobTypeTakingJobOver_KeepsTheTakeoverOnTheRescheduledTrigger()
    {
        // Arrange — a re-fire scheduled to take over an unrecorded attempt lands on a node without the handler.
        var context = FiringOf(JobId, jobType: "Orphan");
        context.MergedJobDataMap.Returns(new JobDataMap
        {
            [BackgroundJobExecution.JobIdKey] = JobId.ToString(),
            [BackgroundJobExecution.ReclaimKey] = bool.TrueString,
        });

        // Act
        var rescheduled = await DeclinedTriggerOfAsync(context);

        // Assert — the next firing can still re-claim the row the earlier attempt left Processing.
        rescheduled.JobDataMap.GetString(BackgroundJobExecution.ReclaimKey).Should().Be(bool.TrueString);
        rescheduled.JobDataMap.GetString(BackgroundJobExecution.JobIdKey).Should().Be(JobId.ToString());
    }

    [Fact]
    public async Task Execute_UnknownJobTypeOnLegacyTriggerWithRetryPolicy_ReschedulesTheTriggerWithoutThePolicy()
    {
        // Arrange — a trigger an earlier version stored, with a scheduler retry policy, lands on a node without the
        // handler.
        var context = LegacyJobTriggerFiringOf(JobId);
        context.JobDetail.Returns(QuartzRegistration.DurableJobFor("Orphan"));

        // Act
        var rescheduled = await DeclinedTriggerOfAsync(context);

        // Assert
        rescheduled.RetryPolicy.Should().BeNull();
    }

    [Fact]
    public async Task Execute_UnknownJobTypeRecovered_MarksTheRescheduledTriggerToTakeTheJobOver()
    {
        // Arrange — Quartz recovered a dead node's firing onto a node without the handler.
        var context = FiringOf(JobId, jobType: "Orphan");
        context.Recovering.Returns(true);

        // Act
        var rescheduled = await DeclinedTriggerOfAsync(context);

        // Assert — the rescheduled trigger is no recovery firing, so it has to say it takes the job over.
        rescheduled.JobDataMap.GetString(BackgroundJobExecution.ReclaimKey).Should().Be(bool.TrueString);
    }

    [Fact]
    public async Task Execute_UnknownJobTypeWhileSchedulerStops_LeavesTheFiringToRecoveryWithoutThrowing()
    {
        // Arrange — the stopping scheduler refuses the declined trigger.
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = FiringOf(JobId, jobType: "Orphan");
        context.Scheduler.Status.Returns(SchedulerStatus.Shutdown);
        context.Scheduler
            .RescheduleJob(Arg.Any<TriggerKey>(), Arg.Any<ITrigger>(), Arg.Any<CancellationToken>())
            .Returns<DateTimeOffset?>(_ => throw new SchedulerException("The scheduler is shutting down."));

        // Act
        var act = async () => await execution.Execute(context, TestContext.Current.CancellationToken);

        // Assert — the firing ends quietly, and the row is never touched.
        await act.Should().NotThrowAsync();
        repository.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task Execute_FiringWithoutJobId_TouchesNothing()
    {
        // Arrange — a durable job fired by hand carries no job id.
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        await using var provider = Services(repository, new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        var context = FiringOf(JobId);
        context.MergedJobDataMap.Returns(new JobDataMap());

        // Act
        var act = async () => await execution.Execute(context, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
        repository.ReceivedCalls().Should().BeEmpty();
        context.Scheduler.ReceivedCalls().Should().BeEmpty();
    }

    private static async Task<ITrigger> DeclinedTriggerOfAsync(IJobExecutionContext context)
    {
        await using var provider = Services(Substitute.For<IBackgroundJobStateRepository>(), new ObservedRun());
        var execution = ActivatorUtilities.CreateInstance<BackgroundJobExecution>(provider);
        ITrigger? rescheduled = null;
        context.Scheduler
            .RescheduleJob(Arg.Any<TriggerKey>(), Arg.Do<ITrigger>(trigger => rescheduled = trigger), Arg.Any<CancellationToken>())
            .Returns(DateTimeOffset.UtcNow);

        await execution.Execute(context, TestContext.Current.CancellationToken);

        return rescheduled ?? throw new InvalidOperationException("The firing was not rescheduled.");
    }
}
