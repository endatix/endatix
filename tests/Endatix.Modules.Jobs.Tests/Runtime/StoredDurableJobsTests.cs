using Endatix.Modules.Jobs.Runtime;
using Npgsql;
using NSubstitute.ExceptionExtensions;
using Quartz;

namespace Endatix.Modules.Jobs.Tests.Runtime;

public sealed class StoredDurableJobsTests
{
    private const string JobType = "WebHookDelivery";
    private static readonly JobKey JobKey = QuartzRegistration.JobKeyFor(JobType);

    [Fact]
    public async Task EnsureAsync_JobTypeRemembered_DoesNotAskTheStore()
    {
        // Arrange
        var scheduler = Substitute.For<IScheduler>();
        var durableJobs = new StoredDurableJobs();
        durableJobs.Remember([JobType]);

        // Act
        await durableJobs.EnsureAsync(scheduler, [JobType], CancellationToken.None);

        // Assert
        await scheduler.DidNotReceiveWithAnyArgs().Exists(JobKey, CancellationToken.None);
        await scheduler.DidNotReceiveWithAnyArgs().AddJob(null!, default, CancellationToken.None);
    }

    [Fact]
    public async Task EnsureAsync_JobTypeNotRememberedAndMissing_StoresTheDurableJob()
    {
        // Arrange
        var scheduler = Substitute.For<IScheduler>();
        scheduler.Exists(JobKey, Arg.Any<CancellationToken>()).Returns(false);
        var durableJobs = new StoredDurableJobs();

        // Act
        await durableJobs.EnsureAsync(scheduler, [JobType], CancellationToken.None);

        // Assert
        await scheduler.Received(1).AddJob(
            Arg.Is<IJobDetail>(job => job.Key.Equals(JobKey) && job.Durable), AddJobOptions.Replacing, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScheduleAsync_RememberedJobMissingFromTheStore_StoresItAgainAndSchedulesOnceMore()
    {
        // Arrange
        var scheduler = Substitute.For<IScheduler>();
        var trigger = Trigger();
        scheduler.ScheduleJob(trigger, Arg.Any<ScheduleJobOptions>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => throw new JobPersistenceException("The job referenced by the trigger does not exist."),
                _ => DateTimeOffset.UtcNow);
        scheduler.Exists(JobKey, Arg.Any<CancellationToken>()).Returns(false);
        var durableJobs = new StoredDurableJobs();
        durableJobs.Remember([JobType]);

        // Act
        await durableJobs.ScheduleAsync(scheduler, trigger, CancellationToken.None);

        // Assert
        await scheduler.Received(1).AddJob(
            Arg.Is<IJobDetail>(job => job.Key.Equals(JobKey)), AddJobOptions.Replacing, Arg.Any<CancellationToken>());
        await scheduler.Received(2).ScheduleJob(trigger, Arg.Any<ScheduleJobOptions>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ScheduleAsync_RememberedJobStillStored_RethrowsWithoutStoringIt()
    {
        // Arrange
        var scheduler = Substitute.For<IScheduler>();
        var trigger = Trigger();
        scheduler.ScheduleJob(trigger, Arg.Any<ScheduleJobOptions>(), Arg.Any<CancellationToken>())
            .Throws(new JobPersistenceException("Couldn't store trigger."));
        scheduler.Exists(JobKey, Arg.Any<CancellationToken>()).Returns(true);
        var durableJobs = new StoredDurableJobs();
        durableJobs.Remember([JobType]);

        // Act
        var schedule = () => durableJobs.ScheduleAsync(scheduler, trigger, CancellationToken.None);

        // Assert
        await schedule.Should().ThrowAsync<JobPersistenceException>();
        await scheduler.DidNotReceiveWithAnyArgs().AddJob(null!, default, CancellationToken.None);
    }

    [Fact]
    public async Task ScheduleAsync_DatabaseFailure_RethrowsWithoutAskingTheStore()
    {
        // Arrange — the transaction a database error leaves behind takes no further statements.
        var scheduler = Substitute.For<IScheduler>();
        var trigger = Trigger();
        scheduler.ScheduleJob(trigger, Arg.Any<ScheduleJobOptions>(), Arg.Any<CancellationToken>())
            .Throws(new JobPersistenceException("Couldn't store trigger.", new NpgsqlException("connection lost")));
        var durableJobs = new StoredDurableJobs();
        durableJobs.Remember([JobType]);

        // Act
        var schedule = () => durableJobs.ScheduleAsync(scheduler, trigger, CancellationToken.None);

        // Assert
        await schedule.Should().ThrowAsync<JobPersistenceException>();
        await scheduler.DidNotReceiveWithAnyArgs().Exists(JobKey, CancellationToken.None);
    }

    private static ITrigger Trigger() =>
        QuartzRegistration.TriggerFor(new JobTriggerSpec(42, JobType));
}
