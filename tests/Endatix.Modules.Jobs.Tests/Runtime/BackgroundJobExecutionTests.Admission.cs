using Endatix.Core.Abstractions;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

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
}
