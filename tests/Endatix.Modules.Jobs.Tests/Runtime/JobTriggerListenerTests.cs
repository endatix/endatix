using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Quartz;
using static Endatix.Modules.Jobs.Tests.Runtime.JobExecutionTestHost;

namespace Endatix.Modules.Jobs.Tests.Runtime;

/// <summary>
/// A trigger stored by an earlier version with a scheduler retry policy, whose retries ran out: the job is taken over
/// while its row is unfinished.
/// </summary>
public sealed class JobTriggerListenerTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TriggerRetriesExhausted_RowStillRetrying_ReschedulesTheTriggerToTakeTheJobOver()
    {
        // Arrange
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository.ReadStatusAsync(JobId, Arg.Any<CancellationToken>()).Returns(JobStatus.Retrying);
        var context = LegacyJobTriggerFiringOf(JobId);
        var rescheduled = CaptureRescheduled(context);

        // Act
        await RetriesExhaustedAsync(repository, context);

        // Assert — the job's own key, so it replaces the trigger that fired rather than adding a second.
        rescheduled.Should().ContainSingle();
        rescheduled[0].Key.Should().Be(new TriggerKey(JobId.ToString(), ProbeJobType));
        rescheduled[0].JobDataMap.GetString(BackgroundJobExecution.ReclaimKey).Should().Be(bool.TrueString);
        rescheduled[0].StartTimeUtc.Should().Be(Now.Add(UnrecordedJobRefire.Delay));
        rescheduled[0].RetryPolicy.Should().BeNull();
    }

    [Theory]
    [InlineData(JobStatus.Completed)]
    [InlineData(JobStatus.Failed)]
    [InlineData(JobStatus.DeadLettered)]
    [InlineData(JobStatus.Canceled)]
    public async Task TriggerRetriesExhausted_RowFinished_SchedulesNothing(JobStatus status)
    {
        // Arrange
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository.ReadStatusAsync(JobId, Arg.Any<CancellationToken>()).Returns(status);
        var context = LegacyJobTriggerFiringOf(JobId);

        // Act
        await RetriesExhaustedAsync(repository, context);

        // Assert
        context.Scheduler.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task TriggerRetriesExhausted_RowGone_SchedulesNothing()
    {
        // Arrange
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository.ReadStatusAsync(JobId, Arg.Any<CancellationToken>()).Returns((JobStatus?)null);
        var context = LegacyJobTriggerFiringOf(JobId);

        // Act
        await RetriesExhaustedAsync(repository, context);

        // Assert
        context.Scheduler.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task TriggerRetriesExhausted_StatusCannotBeRead_TakesTheJobOver()
    {
        // Arrange — a take-over of a finished row finds nothing to claim, so not knowing is no reason to strand it.
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository.ReadStatusAsync(JobId, Arg.Any<CancellationToken>())
            .Returns<Task<JobStatus?>>(_ => throw new TimeoutException("The database did not answer."));
        var context = LegacyJobTriggerFiringOf(JobId);
        var rescheduled = CaptureRescheduled(context);

        // Act
        await RetriesExhaustedAsync(repository, context);

        // Assert
        rescheduled.Should().ContainSingle()
            .Which.JobDataMap.GetString(BackgroundJobExecution.ReclaimKey).Should().Be(bool.TrueString);
    }

    private static async Task RetriesExhaustedAsync(IBackgroundJobStateRepository repository, IJobExecutionContext context)
    {
        await using var provider = Services(repository, new ObservedRun());
        provider.GetRequiredService<IDateTimeProvider>().UtcNow.Returns(Now);
        var listener = ActivatorUtilities.CreateInstance<JobTriggerListener>(provider);
        await listener.TriggerRetriesExhausted(
            context.Trigger,
            context,
            new JobExecutionException("Background job attempt failed."),
            TestContext.Current.CancellationToken);
    }

    private static List<ITrigger> CaptureRescheduled(IJobExecutionContext context)
    {
        var rescheduled = new List<ITrigger>();
        context.Scheduler
            .RescheduleJob(context.Trigger.Key, Arg.Do<ITrigger>(rescheduled.Add), Arg.Any<CancellationToken>())
            .Returns(Now.Add(UnrecordedJobRefire.Delay));
        return rescheduled;
    }
}
