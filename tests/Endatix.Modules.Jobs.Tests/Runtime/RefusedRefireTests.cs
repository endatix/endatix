using System.Data.Common;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Runtime;
using Endatix.Modules.Jobs.Tests.Shared;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using Quartz;
using static Endatix.Modules.Jobs.Tests.Runtime.JobExecutionTestHost;

namespace Endatix.Modules.Jobs.Tests.Runtime;

/// <summary>
/// Which refused trigger stores are the database's, to be tried until they land, and how a firing that gives up on one
/// settles the row.
/// </summary>
public sealed class RefusedRefireTests
{
    public static TheoryData<Exception> DatabaseFailures() => new()
    {
        new TimeoutException("The command timed out."),
        new JobPersistenceException("Couldn't store trigger.", new NpgsqlException("Connection refused.")),
        new JobPersistenceException("Couldn't store trigger.", new TimeoutException("The pool is exhausted.")),
        new SchedulerException("Couldn't store trigger.", new JobPersistenceException("Lock failed.", new TestDbException())),
    };

    public static TheoryData<Exception> SchedulerRefusals() => new()
    {
        new JobPersistenceException("The job (endatix.TenantProbe) referenced by the trigger does not exist."),
        new SchedulerException("The trigger will never fire."),
        new JobPersistenceException("Couldn't store trigger because the BLOB couldn't be serialized.", new IOException("Bad data.")),
        new InvalidOperationException("Unexpected."),
    };

    [Theory]
    [MemberData(nameof(DatabaseFailures))]
    public void IsTransient_FailureCausedByTheDatabase_ReturnsTrue(Exception failure)
    {
        // Act
        var transient = RefusedRefire.IsTransient(failure);

        // Assert
        transient.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(SchedulerRefusals))]
    public void IsTransient_RefusalOfTheSchedulersOwn_ReturnsFalse(Exception failure)
    {
        // Act
        var transient = RefusedRefire.IsTransient(failure);

        // Assert
        transient.Should().BeFalse();
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task GiveUpAsync_CarriedOutcome_LogsItWrittenOnlyWhenTheWriteLanded(bool lands, int writtenLogs)
    {
        // Arrange — the scheduler has refused the re-fire for the last time; the carried completion's fenced write
        // lands, or finds the row cancelled, taken over or recorded meanwhile.
        var carried = new UnsavedOutcome(2, AttemptRowWrite.Completed, null, DateTime.UtcNow, DateTime.UtcNow);
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository.TryCompleteAsync(new AttemptRef(JobId, carried.Attempt), Arg.Any<JobFinish>(), Arg.Any<CancellationToken>())
            .Returns(lands);
        await using var provider = Services(repository, new ObservedRun());
        var logger = new RecordingLogger<RefusedRefire>();
        var refused = ActivatorUtilities.CreateInstance<RefusedRefire>(provider, logger);
        var unstored = LastRefusalDue(carried);
        var refusal = SchedulerRefusalOf(carried);

        // Act
        var settled = await refused.GiveUpAsync(refusal, unstored);

        // Assert
        settled.Should().BeTrue();
        logger.Entries
            .Count(entry => entry.Level == LogLevel.Error && entry.Message.Contains("so the firing wrote the Completed outcome"))
            .Should().Be(writtenLogs);
    }

    [Fact]
    public async Task GiveUpAsync_CarriedOutcomeRejected_KeepsTheFiringOpenUntilTheRejectionsAreSpent()
    {
        // Arrange — the carried completion's write breaks a rule of the table while the row still reads, and the
        // outcome has one rejection left before this firing's.
        var carried = new UnsavedOutcome(2, AttemptRowWrite.Completed, null, DateTime.UtcNow, DateTime.UtcNow)
            with { Rejections = UnsavedOutcome.MaxRejections - 2 };
        var repository = Substitute.For<IBackgroundJobStateRepository>();
        repository.TryCompleteAsync(Arg.Any<AttemptRef>(), Arg.Any<JobFinish>(), Arg.Any<CancellationToken>())
            .Returns<Task<bool>>(_ => throw new InvalidOperationException("The row breaks a constraint."));
        repository.ReadAttemptAsync(JobId, Arg.Any<CancellationToken>())
            .Returns(new JobAttemptState(JobStatus.Processing, carried.Attempt));
        repository.TryDeadLetterAsync(Arg.Any<UnfinishedRow>(), Arg.Any<AttemptFailure>(), Arg.Any<CancellationToken>())
            .Returns(true);
        await using var provider = Services(repository, new ObservedRun());
        var refused = ActivatorUtilities.CreateInstance<RefusedRefire>(provider);
        var unstored = LastRefusalDue(carried);
        var refusal = SchedulerRefusalOf(carried);

        // Act — the scheduler refuses the trigger again after the first rejection.
        var settledFirst = await refused.GiveUpAsync(refusal, unstored);
        var settledThen = await refused.GiveUpAsync(refusal, unstored);

        // Assert
        settledFirst.Should().BeFalse();
        settledThen.Should().BeTrue();
        await repository.Received(2 * (JobOutcomeRecorder.WriteRetryDelays.Length + 1))
            .TryCompleteAsync(new AttemptRef(JobId, carried.Attempt), Arg.Any<JobFinish>(), CancellationToken.None);
        await repository.Received(1).TryDeadLetterAsync(
            new UnfinishedRow(JobId, JobStatus.Processing, carried.Attempt), Arg.Any<AttemptFailure>(), Arg.Any<CancellationToken>());
    }

    private static RefireRefusal SchedulerRefusalOf(UnsavedOutcome carried) =>
        new(
            Substitute.For<IScheduler>(),
            new JobRefireTrigger(JobId, UnrecordedJobRefire.Delay, (_, _) => null!, carried.Attempt, carried),
            QuartzRegistration.JobKeyFor(ProbeJobType),
            new SchedulerException("The trigger will never fire."),
            DateTime.UtcNow);

    // A trigger carrying the outcome that the scheduler has refused one time short of giving up on it.
    private static UnstoredTrigger LastRefusalDue(UnsavedOutcome carried)
    {
        var unstored = new UnstoredTrigger(QuickTriggerStoreRetries, carried);
        for (var refusal = 1; refusal < RefusedRefire.MaxRefusals; refusal++)
        {
            unstored.RecordRefusal();
        }

        return unstored;
    }

    private sealed class TestDbException() : DbException("The server closed the connection.");
}
