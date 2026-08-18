using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Domain;

namespace Endatix.Modules.Jobs.Tests.Domain;

/// <summary>
/// Covers the job state machine. The runner's hot paths write the same columns through
/// <c>ExecuteUpdateAsync</c> rather than through these methods, so these tests are where the rules
/// themselves are pinned — if a rule changes here, the runner's SQL has to change with it.
/// </summary>
public class BackgroundJobTests
{
    private static readonly DateTime Now = new(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);

    private static BackgroundJob NewJob() =>
        new("SubmissionExport", """{"formId":"1"}""", tenantId: 7, nextAttemptAt: Now);

    private static BackgroundJob ClaimedJob()
    {
        var job = NewJob();
        job.Claim(Now);
        return job;
    }

    [Fact]
    public void Constructor_ValidArguments_StartsPendingAndUnattempted()
    {
        // Arrange
        // Act
        var job = NewJob();

        // Assert
        job.Status.Should().Be(JobStatus.Pending);
        job.AttemptCount.Should().Be(0);
        job.NextAttemptAt.Should().Be(Now);
        job.IsTerminal.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_BlankJobType_Throws(string jobType)
    {
        // Arrange
        // Act
        var act = () => new BackgroundJob(jobType, "{}", tenantId: 7, nextAttemptAt: Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_NegativeTenantId_Throws()
    {
        // Arrange
        // Act
        var act = () => new BackgroundJob("SubmissionExport", "{}", tenantId: -1, nextAttemptAt: Now);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Constructor_DefaultTenantId_IsAllowed()
    {
        // Arrange
        // Act — tenant 0 is the app-level tenant, valid for cross-tenant system work.
        var job = new BackgroundJob("SubmissionExport", "{}", tenantId: 0, nextAttemptAt: Now);

        // Assert
        job.TenantId.Should().Be(0);
    }

    [Fact]
    public void IsEligible_PendingAndDue_ReturnsTrue()
    {
        // Arrange
        var job = NewJob();

        // Act
        var eligible = job.IsEligible(Now);

        // Assert
        eligible.Should().BeTrue();
    }

    [Fact]
    public void IsEligible_BackoffNotElapsed_ReturnsFalse()
    {
        // Arrange
        var job = ClaimedJob();
        job.Reschedule(Now.AddMinutes(5));

        // Act
        var eligible = job.IsEligible(Now);

        // Assert
        eligible.Should().BeFalse();
    }

    [Fact]
    public void Claim_PendingJob_ConsumesAnAttemptAndStartsHeartbeat()
    {
        // Arrange
        var job = NewJob();

        // Act
        job.Claim(Now);

        // Assert
        job.Status.Should().Be(JobStatus.Processing);
        job.AttemptCount.Should().Be(1);
        job.StartedAt.Should().Be(Now);
        job.HeartbeatAt.Should().Be(Now);
    }

    [Fact]
    public void Claim_SecondAttempt_KeepsOriginalStartedAt()
    {
        // Arrange — a job that failed once and is being retried.
        var job = ClaimedJob();
        job.Reschedule(Now);
        var retryAt = Now.AddMinutes(1);

        // Act
        job.Claim(retryAt);

        // Assert — StartedAt marks when the work first began, not when the latest attempt did.
        job.StartedAt.Should().Be(Now);
        job.AttemptCount.Should().Be(2);
    }

    [Fact]
    public void Claim_BeforeBackoffElapsed_Throws()
    {
        // Arrange — a job waiting out a retry backoff.
        var job = ClaimedJob();
        job.Reschedule(Now.AddMinutes(5));

        // Act
        var act = () => job.Claim(Now);

        // Assert — the entity must refuse for the same reason the claim query would not match it,
        // so an early retry cannot be started in memory and consume an attempt.
        act.Should().Throw<InvalidOperationException>();
        job.Status.Should().Be(JobStatus.Retrying);
        job.AttemptCount.Should().Be(1);
    }

    [Fact]
    public void Claim_OnceBackoffElapsed_Succeeds()
    {
        // Arrange
        var job = ClaimedJob();
        var dueAt = Now.AddMinutes(5);
        job.Reschedule(dueAt);

        // Act
        job.Claim(dueAt);

        // Assert
        job.Status.Should().Be(JobStatus.Processing);
        job.AttemptCount.Should().Be(2);
    }

    [Fact]
    public void Complete_AfterAFailedAttempt_ClearsTheStaleError()
    {
        // Arrange — first attempt failed retryably and recorded why.
        var job = ClaimedJob();
        job.Reschedule(Now, "Connection reset");
        job.Claim(Now);

        // Act
        job.Complete(Now);

        // Assert — the job succeeded, so it must not still surface the earlier failure.
        job.Status.Should().Be(JobStatus.Completed);
        job.ErrorMessage.Should().BeNull();
    }

    [Fact]
    public void Claim_AlreadyProcessing_Throws()
    {
        // Arrange
        var job = ClaimedJob();

        // Act
        var act = () => job.Claim(Now);

        // Assert — the CAS is the real guard across processes; this catches the same bug in-process.
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Complete_ProcessingJob_IsTerminalAtFullProgress()
    {
        // Arrange
        var job = ClaimedJob();

        // Act
        job.Complete(Now, """{"downloadPath":"a.csv"}""");

        // Assert
        job.Status.Should().Be(JobStatus.Completed);
        job.ProgressPercentage.Should().Be(100);
        job.CompletedAt.Should().Be(Now);
        job.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void Fail_ProcessingJob_IsTerminalWithoutFurtherAttempts()
    {
        // Arrange
        var job = ClaimedJob();

        // Act — a deterministic failure: retrying cannot help.
        job.Fail("Form schema is not compiled", Now);

        // Assert
        job.Status.Should().Be(JobStatus.Failed);
        job.ErrorMessage.Should().Be("Form schema is not compiled");
        job.AttemptCount.Should().Be(1);
        job.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void Reschedule_ProcessingJob_BecomesEligibleAgainWithoutExtraAttempt()
    {
        // Arrange
        var job = ClaimedJob();
        var nextAttempt = Now.AddSeconds(30);

        // Act
        job.Reschedule(nextAttempt, "Timeout");

        // Assert — the attempt was consumed at Claim; rescheduling must not double-count it.
        job.Status.Should().Be(JobStatus.Retrying);
        job.AttemptCount.Should().Be(1);
        job.NextAttemptAt.Should().Be(nextAttempt);
        job.IsTerminal.Should().BeFalse();
    }

    [Fact]
    public void Reschedule_ProcessingJob_ClearsHeartbeat()
    {
        // Arrange
        var job = ClaimedJob();

        // Act
        job.Reschedule(Now.AddSeconds(30));

        // Assert — a waiting job has no live worker, so a stale heartbeat must not linger and make
        // the stale-reaper treat it as an abandoned in-flight job.
        job.HeartbeatAt.Should().BeNull();
    }

    [Fact]
    public void DeadLetter_ProcessingJob_IsTerminalAndDistinctFromFailed()
    {
        // Arrange
        var job = ClaimedJob();

        // Act
        job.DeadLetter("Endpoint unreachable", Now);

        // Assert
        job.Status.Should().Be(JobStatus.DeadLettered);
        job.Status.Should().NotBe(JobStatus.Failed);
        job.IsTerminal.Should().BeTrue();
    }

    [Theory]
    [InlineData(JobStatus.Pending)]
    [InlineData(JobStatus.Retrying)]
    [InlineData(JobStatus.Processing)]
    public void Cancel_NonTerminalJob_Succeeds(JobStatus startingStatus)
    {
        // Arrange
        var job = NewJob();
        if (startingStatus is JobStatus.Retrying)
        {
            job.Claim(Now);
            job.Reschedule(Now);
        }
        else if (startingStatus is JobStatus.Processing)
        {
            job.Claim(Now);
        }

        // Act
        job.Cancel(Now);

        // Assert
        job.Status.Should().Be(JobStatus.Canceled);
        job.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void Cancel_CompletedJob_Throws()
    {
        // Arrange
        var job = ClaimedJob();
        job.Complete(Now);

        // Act
        var act = () => job.Cancel(Now);

        // Assert — terminal states are immutable.
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void ReportProgress_ProcessingJob_UpdatesDisplayFieldsOnly()
    {
        // Arrange
        var job = ClaimedJob();
        var heartbeatBefore = job.HeartbeatAt;

        // Act
        job.ReportProgress(45, "Processing 4,500 of 10,000 rows");

        // Assert — progress is a user-facing courtesy, never the liveness signal.
        job.ProgressPercentage.Should().Be(45);
        job.StatusMessage.Should().Be("Processing 4,500 of 10,000 rows");
        job.HeartbeatAt.Should().Be(heartbeatBefore);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void ReportProgress_OutOfRangePercentage_Throws(int percentage)
    {
        // Arrange
        var job = ClaimedJob();

        // Act
        var act = () => job.ReportProgress(percentage);

        // Assert
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void ReportProgress_PendingJob_Throws()
    {
        // Arrange
        var job = NewJob();

        // Act
        var act = () => job.ReportProgress(10);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Heartbeat_ProcessingJob_AdvancesLivenessWithoutTouchingProgress()
    {
        // Arrange
        var job = ClaimedJob();
        job.ReportProgress(20);
        var later = Now.AddSeconds(30);

        // Act
        job.Heartbeat(later);

        // Assert — a handler that reports nothing for minutes must still read as alive.
        job.HeartbeatAt.Should().Be(later);
        job.ProgressPercentage.Should().Be(20);
    }

    [Fact]
    public void Heartbeat_PendingJob_Throws()
    {
        // Arrange
        var job = NewJob();

        // Act
        var act = () => job.Heartbeat(Now);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }
}
