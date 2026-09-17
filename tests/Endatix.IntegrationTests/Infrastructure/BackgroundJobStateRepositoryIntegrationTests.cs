using System.Diagnostics;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Domain;
using Endatix.Modules.Jobs.Persistence;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>
/// The job state repository against PostgreSQL: its writes are conditional <c>ExecuteUpdate</c>
/// statements, which only a real database runs.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class BackgroundJobStateRepositoryIntegrationTests(EndatixIntegrationWebHostFixture fixture)
{
    private const string SkipReason =
        "Background jobs are PostgreSQL-only; the module is not registered on this provider.";

    private const string KnownJobType = "Known";
    private const string UnregisteredJobType = "Unregistered";
    private const string PayloadJson = """{"formId":"1"}""";
    private const string FailureMessage = "Form 42 has no schema.";
    private const string RetryMessage = "The job could not be completed.";
    private const string ReapedMessage = "The job stopped responding and was presumed lost.";

    // High, unseeded ids so these rows cannot be confused with a tenant the standard seed created.
    private const long FirstTenantId = 9301;
    private const long SecondTenantId = 9302;

    private static readonly string[] RegisteredJobTypes = [KnownJobType];

    private static readonly DateTime Now = new(2026, 9, 15, 12, 0, 0, DateTimeKind.Utc);

    private static readonly DateTime Earlier = Now.AddMinutes(-1);

    // Stamped on every seeded row, so a write that touches ModifiedAt is detectable.
    private static readonly DateTime SeededModifiedAt = Now.AddDays(-1);

    private static readonly TimeSpan LockWaitTimeout = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task TryClaimAsync_EligibleJob_ClaimsAndConsumesAttempt()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = fixture.Factory.Services.CreateScope();
        var context = JobsContext(scope);
        await ClearJobsAsync(context, cancellationToken);
        var jobId = await SeedAsync(context, cancellationToken, nextAttemptAt: Now);
        var repository = new BackgroundJobStateRepository(context);

        // Act
        var claimed = await repository.TryClaimAsync(jobId, RegisteredJobTypes, Now, cancellationToken);

        // Assert
        claimed.Should().NotBeNull();
        claimed!.AttemptCount.Should().Be(1);
        var job = await ReadAsync(context, jobId, cancellationToken);
        job.Status.Should().Be(JobStatus.Processing);
        job.AttemptCount.Should().Be(1);
        job.StartedAt.Should().Be(Now);
        job.HeartbeatAt.Should().Be(Now);
        claimed.Should().Be(new ClaimedJob(
            job.Id, job.JobType, job.TenantId, job.PayloadJson, job.AttemptCount, job.TraceId, job.Status));
    }

    [Fact]
    public async Task TryClaimAsync_AlreadyClaimed_ReturnsNull()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = fixture.Factory.Services.CreateScope();
        var context = JobsContext(scope);
        await ClearJobsAsync(context, cancellationToken);
        var jobId = await SeedAsync(context, cancellationToken, nextAttemptAt: Now);
        var repository = new BackgroundJobStateRepository(context);
        (await repository.TryClaimAsync(jobId, RegisteredJobTypes, Now, cancellationToken)).Should().NotBeNull();

        // Act
        var reclaimed = await repository.TryClaimAsync(jobId, RegisteredJobTypes, Now, cancellationToken);

        // Assert
        reclaimed.Should().BeNull();
        var job = await ReadAsync(context, jobId, cancellationToken);
        job.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task TryClaimAsync_NotDueOrCanceled_ReturnsNull()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = fixture.Factory.Services.CreateScope();
        var context = JobsContext(scope);
        await ClearJobsAsync(context, cancellationToken);
        var notDueId = await SeedAsync(
            context,
            cancellationToken,
            status: JobStatus.Retrying,
            nextAttemptAt: Now.AddSeconds(60));
        var canceledId = await SeedAsync(context, cancellationToken, status: JobStatus.Canceled);
        var repository = new BackgroundJobStateRepository(context);

        // Act
        var claimedNotDue = await repository.TryClaimAsync(notDueId, RegisteredJobTypes, Now, cancellationToken);
        var claimedCanceled = await repository.TryClaimAsync(canceledId, RegisteredJobTypes, Now, cancellationToken);

        // Assert
        claimedNotDue.Should().BeNull();
        claimedCanceled.Should().BeNull();

        var notDue = await ReadAsync(context, notDueId, cancellationToken);
        notDue.Status.Should().Be(JobStatus.Retrying);
        notDue.AttemptCount.Should().Be(0);
        notDue.StartedAt.Should().BeNull();
        notDue.HeartbeatAt.Should().BeNull();
        notDue.NextAttemptAt.Should().Be(Now.AddSeconds(60));
        notDue.ModifiedAt.Should().Be(SeededModifiedAt);
    }

    [Fact]
    public async Task TryClaimAsync_UnregisteredJobType_ReturnsNull()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = fixture.Factory.Services.CreateScope();
        var context = JobsContext(scope);
        await ClearJobsAsync(context, cancellationToken);
        var jobId = await SeedAsync(context, cancellationToken, jobType: UnregisteredJobType);
        var repository = new BackgroundJobStateRepository(context);

        // Act
        var claimed = await repository.TryClaimAsync(jobId, RegisteredJobTypes, Now, cancellationToken);

        // Assert — left untouched, so an instance that handles the type still has the full attempt budget.
        claimed.Should().BeNull();
        var job = await ReadAsync(context, jobId, cancellationToken);
        job.Status.Should().Be(JobStatus.Pending);
        job.AttemptCount.Should().Be(0);
        job.StartedAt.Should().BeNull();
        job.HeartbeatAt.Should().BeNull();
        job.ModifiedAt.Should().Be(SeededModifiedAt);
    }

    [Fact]
    public async Task TryClaimAsync_Retry_KeepsStartedAt()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = fixture.Factory.Services.CreateScope();
        var context = JobsContext(scope);
        await ClearJobsAsync(context, cancellationToken);
        var firstStartedAt = Now.AddMinutes(-30);
        var jobId = await SeedAsync(
            context,
            cancellationToken,
            status: JobStatus.Retrying,
            nextAttemptAt: Now,
            attemptCount: 1,
            startedAt: firstStartedAt);
        var repository = new BackgroundJobStateRepository(context);

        // Act
        var claimed = await repository.TryClaimAsync(jobId, RegisteredJobTypes, Now, cancellationToken);

        // Assert
        claimed.Should().NotBeNull();
        claimed!.AttemptCount.Should().Be(2);
        var job = await ReadAsync(context, jobId, cancellationToken);
        job.AttemptCount.Should().Be(2);
        job.StartedAt.Should().Be(firstStartedAt);
    }

    [Fact]
    public async Task TryClaimAsync_ConcurrentClaims_OneWins()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        using var seedScope = fixture.Factory.Services.CreateScope();
        var seedContext = JobsContext(seedScope);
        await ClearJobsAsync(seedContext, cancellationToken);
        var jobId = await SeedAsync(seedContext, cancellationToken, nextAttemptAt: Now);

        // One scope each: a shared context would serialize the two claims in the client.
        using var firstScope = fixture.Factory.Services.CreateScope();
        using var secondScope = fixture.Factory.Services.CreateScope();
        var first = new BackgroundJobStateRepository(JobsContext(firstScope));
        var second = new BackgroundJobStateRepository(JobsContext(secondScope));

        // Act
        var claims = await Task.WhenAll(
            first.TryClaimAsync(jobId, RegisteredJobTypes, Now, cancellationToken),
            second.TryClaimAsync(jobId, RegisteredJobTypes, Now, cancellationToken));

        // Assert
        claims.Should().ContainSingle(claimed => claimed != null)
            .Which!.AttemptCount.Should().Be(1);
        var job = await ReadAsync(seedContext, jobId, cancellationToken);
        job.AttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task TryClaimAsync_AttemptCountChangedSinceRead_ReturnsNull()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = fixture.Factory.Services.CreateScope();
        var context = JobsContext(scope);
        await ClearJobsAsync(context, cancellationToken);
        var jobId = await SeedAsync(context, cancellationToken, nextAttemptAt: Now);

        using var claimScope = fixture.Factory.Services.CreateScope();
        var repository = new BackgroundJobStateRepository(JobsContext(claimScope));

        // A second connection holds the row lock, so the claim's read goes through and its update waits.
        using var lockScope = fixture.Factory.Services.CreateScope();
        var lockContext = JobsContext(lockScope);
        await using var transaction = await lockContext.Database.BeginTransactionAsync(cancellationToken);
        await lockContext.Database.ExecuteSqlAsync(
            $"""SELECT "Id" FROM jobs."BackgroundJobs" WHERE "Id" = {jobId} FOR UPDATE""",
            cancellationToken);
        var lockHolderPid = await lockContext.Database
            .SqlQuery<int>($"""SELECT pg_backend_pid() AS "Value" """)
            .SingleAsync(cancellationToken);

        // Act
        var claim = Task.Run(
            () => repository.TryClaimAsync(jobId, RegisteredJobTypes, Now, cancellationToken),
            cancellationToken);
        await WaitForUpdateBlockedByAsync(context, lockHolderPid, claim, cancellationToken);

        // Another runner's attempt was claimed and reaped meanwhile: claimable again, one attempt on.
        await lockContext.BackgroundJobs
            .Where(row => row.Id == jobId)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(row => row.Status, JobStatus.Retrying)
                    .SetProperty(row => row.AttemptCount, row => row.AttemptCount + 1)
                    .SetProperty(row => row.StartedAt, (DateTime?)Earlier),
                cancellationToken);
        var movedOn = await ReadAsync(lockContext, jobId, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        var claimed = await claim;

        // Assert — claiming here would hand this runner an attempt it did not take.
        claimed.Should().BeNull();
        var job = await ReadAsync(context, jobId, cancellationToken);
        job.Status.Should().Be(JobStatus.Retrying);
        job.AttemptCount.Should().Be(1);
        job.NextAttemptAt.Should().Be(movedOn.NextAttemptAt);
        job.StartedAt.Should().Be(movedOn.StartedAt);
        job.HeartbeatAt.Should().Be(movedOn.HeartbeatAt);
        job.ModifiedAt.Should().Be(movedOn.ModifiedAt);
    }

    [Fact]
    public async Task TryHeartbeatAsync_StaleAttemptOrCanceled_ReturnsFalse()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = fixture.Factory.Services.CreateScope();
        var context = JobsContext(scope);
        await ClearJobsAsync(context, cancellationToken);
        var beatAt = Now.AddMinutes(5);
        var jobId = await SeedAsync(
            context,
            cancellationToken,
            status: JobStatus.Processing,
            attemptCount: 2,
            startedAt: Now,
            heartbeatAt: Now);
        var repository = new BackgroundJobStateRepository(context);

        // Act
        var owned = await repository.TryHeartbeatAsync(jobId, 2, beatAt, cancellationToken);
        var afterOwnedBeat = await ReadAsync(context, jobId, cancellationToken);
        var staleAttempt = await repository.TryHeartbeatAsync(jobId, 1, Now.AddMinutes(10), cancellationToken);
        var afterStaleBeat = await ReadAsync(context, jobId, cancellationToken);

        await SetStatusAsync(context, jobId, JobStatus.Canceled, cancellationToken);
        var canceled = await repository.TryHeartbeatAsync(jobId, 2, Now.AddMinutes(15), cancellationToken);

        // Assert
        owned.Should().BeTrue();
        afterOwnedBeat.HeartbeatAt.Should().Be(beatAt);

        // A heartbeat is not an edit.
        afterOwnedBeat.ModifiedAt.Should().Be(SeededModifiedAt);

        staleAttempt.Should().BeFalse();
        afterStaleBeat.HeartbeatAt.Should().Be(beatAt);

        canceled.Should().BeFalse();
    }

    [Fact]
    public async Task TryCompleteAsync_CanceledRow_LeavesItCanceled()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = fixture.Factory.Services.CreateScope();
        var context = JobsContext(scope);
        await ClearJobsAsync(context, cancellationToken);
        var runningId = await SeedAsync(
            context,
            cancellationToken,
            status: JobStatus.Processing,
            attemptCount: 2,
            errorMessage: RetryMessage);
        var canceledId = await SeedAsync(
            context,
            cancellationToken,
            status: JobStatus.Canceled,
            attemptCount: 2,
            completedAt: Earlier);
        var repository = new BackgroundJobStateRepository(context);

        // Act
        var completed = await repository.TryCompleteAsync(runningId, 2, Now, cancellationToken);
        var completedCanceled = await repository.TryCompleteAsync(canceledId, 2, Now, cancellationToken);

        // Assert
        completed.Should().BeTrue();
        var running = await ReadAsync(context, runningId, cancellationToken);
        running.Status.Should().Be(JobStatus.Completed);
        running.ProgressPercentage.Should().Be(100);
        running.CompletedAt.Should().Be(Now);
        running.ErrorMessage.Should().BeNull();

        completedCanceled.Should().BeFalse();
        var canceled = await ReadAsync(context, canceledId, cancellationToken);
        canceled.Status.Should().Be(JobStatus.Canceled);
        canceled.CompletedAt.Should().Be(Earlier);
    }

    [Fact]
    public async Task TryFailAsync_LongMessage_Truncates()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = fixture.Factory.Services.CreateScope();
        var context = JobsContext(scope);
        await ClearJobsAsync(context, cancellationToken);
        var shortMessageId = await SeedAsync(
            context,
            cancellationToken,
            status: JobStatus.Processing,
            attemptCount: 1);
        var longMessageId = await SeedAsync(
            context,
            cancellationToken,
            status: JobStatus.Processing,
            attemptCount: 1);
        var repository = new BackgroundJobStateRepository(context);

        // Act
        var failed = await repository.TryFailAsync(shortMessageId, 1, FailureMessage, Now, cancellationToken);
        var failedWithLongMessage = await repository.TryFailAsync(
            longMessageId,
            1,
            new string('a', 3000),
            Now,
            cancellationToken);

        // Assert
        failed.Should().BeTrue();
        var job = await ReadAsync(context, shortMessageId, cancellationToken);
        job.Status.Should().Be(JobStatus.Failed);
        job.ErrorMessage.Should().Be(FailureMessage);
        job.CompletedAt.Should().Be(Now);
        job.AttemptCount.Should().Be(1);

        failedWithLongMessage.Should().BeTrue();
        var truncated = await ReadAsync(context, longMessageId, cancellationToken);
        truncated.ErrorMessage.Should().HaveLength(2048);
    }

    [Fact]
    public async Task RecordFailedAttemptAsync_AttemptsRemainingOrExhausted_RetriesOrDeadLetters()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = fixture.Factory.Services.CreateScope();
        var context = JobsContext(scope);
        await ClearJobsAsync(context, cancellationToken);
        var nextAttemptAt = Now.AddSeconds(30);
        var retryingId = await SeedAsync(
            context,
            cancellationToken,
            status: JobStatus.Processing,
            attemptCount: 1,
            heartbeatAt: Now);
        var exhaustedId = await SeedAsync(
            context,
            cancellationToken,
            status: JobStatus.Processing,
            attemptCount: 3,
            heartbeatAt: Now);
        var repository = new BackgroundJobStateRepository(context);

        // Act
        var retried = await repository.RecordFailedAttemptAsync(
            retryingId, 1, 3, nextAttemptAt, RetryMessage, Now, cancellationToken);
        var deadLettered = await repository.RecordFailedAttemptAsync(
            exhaustedId, 3, 3, nextAttemptAt, RetryMessage, Now, cancellationToken);

        // Assert
        retried.Should().BeTrue();
        var retrying = await ReadAsync(context, retryingId, cancellationToken);
        retrying.Status.Should().Be(JobStatus.Retrying);
        retrying.NextAttemptAt.Should().Be(nextAttemptAt);
        retrying.HeartbeatAt.Should().BeNull();
        retrying.CompletedAt.Should().BeNull();

        // The claim consumed the attempt; recording its failure consumes none.
        retrying.AttemptCount.Should().Be(1);

        deadLettered.Should().BeTrue();
        var exhausted = await ReadAsync(context, exhaustedId, cancellationToken);
        exhausted.Status.Should().Be(JobStatus.DeadLettered);
        exhausted.CompletedAt.Should().Be(Now);
        exhausted.AttemptCount.Should().Be(3);
    }

    [Fact]
    public async Task RecordFailedAttemptAsync_LongMessage_Truncates()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = fixture.Factory.Services.CreateScope();
        var context = JobsContext(scope);
        await ClearJobsAsync(context, cancellationToken);
        var jobId = await SeedAsync(
            context,
            cancellationToken,
            status: JobStatus.Processing,
            attemptCount: 1,
            heartbeatAt: Now);
        var repository = new BackgroundJobStateRepository(context);

        // Act
        var retried = await repository.RecordFailedAttemptAsync(
            jobId, 1, 3, Now.AddSeconds(30), new string('a', 3000), Now, cancellationToken);

        // Assert
        retried.Should().BeTrue();
        var job = await ReadAsync(context, jobId, cancellationToken);
        job.Status.Should().Be(JobStatus.Retrying);
        job.ErrorMessage.Should().HaveLength(2048);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task FailureWriters_NullOrWhiteSpaceMessage_ThrowWithoutWriting(string? errorMessage)
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = fixture.Factory.Services.CreateScope();
        var context = JobsContext(scope);
        await ClearJobsAsync(context, cancellationToken);
        var jobId = await SeedAsync(
            context,
            cancellationToken,
            status: JobStatus.Processing,
            attemptCount: 1,
            heartbeatAt: Now.AddMinutes(-11));
        var before = await ReadAsync(context, jobId, cancellationToken);
        var repository = new BackgroundJobStateRepository(context);

        // Act
        var fail = () => repository.TryFailAsync(jobId, 1, errorMessage!, Now, cancellationToken);
        var recordFailedAttempt = () => repository.RecordFailedAttemptAsync(
            jobId, 1, 3, Now.AddSeconds(30), errorMessage!, Now, cancellationToken);
        var reap = () => repository.TryReapAsync(
            jobId, 1, Now.AddMinutes(-10), 3, Now.AddSeconds(30), errorMessage!, Now, cancellationToken);

        // Assert — the entity's own failure transitions refuse such a message too.
        await fail.Should().ThrowAsync<ArgumentException>();
        await recordFailedAttempt.Should().ThrowAsync<ArgumentException>();
        await reap.Should().ThrowAsync<ArgumentException>();
        var job = await ReadAsync(context, jobId, cancellationToken);
        job.Status.Should().Be(before.Status);
        job.ErrorMessage.Should().Be(before.ErrorMessage);
        job.CompletedAt.Should().Be(before.CompletedAt);
        job.HeartbeatAt.Should().Be(before.HeartbeatAt);
    }

    [Fact]
    public async Task TryReapAsync_FreshHeartbeat_ReturnsFalse()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = fixture.Factory.Services.CreateScope();
        var context = JobsContext(scope);
        await ClearJobsAsync(context, cancellationToken);
        var staleCutoff = Now.AddMinutes(-10);
        var staleId = await SeedAsync(
            context,
            cancellationToken,
            status: JobStatus.Processing,
            attemptCount: 1,
            heartbeatAt: Now.AddMinutes(-11));
        var aliveId = await SeedAsync(
            context,
            cancellationToken,
            status: JobStatus.Processing,
            attemptCount: 1,
            heartbeatAt: Now.AddMinutes(-1));
        var repository = new BackgroundJobStateRepository(context);

        // Act
        var reapedStale = await repository.TryReapAsync(
            staleId, 1, staleCutoff, 3, Now.AddSeconds(30), ReapedMessage, Now, cancellationToken);
        var reapedAlive = await repository.TryReapAsync(
            aliveId, 1, staleCutoff, 3, Now.AddSeconds(30), ReapedMessage, Now, cancellationToken);

        // Assert
        reapedStale.Should().BeTrue();
        (await ReadAsync(context, staleId, cancellationToken)).Status.Should().Be(JobStatus.Retrying);

        // A runner that checked in since the sweeper's scan keeps its job.
        reapedAlive.Should().BeFalse();
        (await ReadAsync(context, aliveId, cancellationToken)).Status.Should().Be(JobStatus.Processing);
    }

    [Fact]
    public async Task TryReapAsync_AttemptsExhausted_DeadLetters()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = fixture.Factory.Services.CreateScope();
        var context = JobsContext(scope);
        await ClearJobsAsync(context, cancellationToken);
        var jobId = await SeedAsync(
            context,
            cancellationToken,
            status: JobStatus.Processing,
            attemptCount: 3,
            heartbeatAt: Now.AddMinutes(-11));
        var repository = new BackgroundJobStateRepository(context);

        // Act
        var reaped = await repository.TryReapAsync(
            jobId, 3, Now.AddMinutes(-10), 3, Now.AddSeconds(30), ReapedMessage, Now, cancellationToken);

        // Assert
        reaped.Should().BeTrue();
        var job = await ReadAsync(context, jobId, cancellationToken);
        job.Status.Should().Be(JobStatus.DeadLettered);
        job.CompletedAt.Should().Be(Now);
        job.ErrorMessage.Should().Be(ReapedMessage);
        job.AttemptCount.Should().Be(3);
    }

    [Fact]
    public async Task TryReapAsync_LongMessage_Truncates()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = fixture.Factory.Services.CreateScope();
        var context = JobsContext(scope);
        await ClearJobsAsync(context, cancellationToken);
        var jobId = await SeedAsync(
            context,
            cancellationToken,
            status: JobStatus.Processing,
            attemptCount: 1,
            heartbeatAt: Now.AddMinutes(-11));
        var repository = new BackgroundJobStateRepository(context);

        // Act
        var reaped = await repository.TryReapAsync(
            jobId, 1, Now.AddMinutes(-10), 3, Now.AddSeconds(30), new string('a', 3000), Now, cancellationToken);

        // Assert
        reaped.Should().BeTrue();
        var job = await ReadAsync(context, jobId, cancellationToken);
        job.Status.Should().Be(JobStatus.Retrying);
        job.ErrorMessage.Should().HaveLength(2048);
    }

    [Fact]
    public async Task FindEligibleAsync_MixedRows_ReturnsOrderedDueIds()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = fixture.Factory.Services.CreateScope();
        var context = JobsContext(scope);
        await ClearJobsAsync(context, cancellationToken);

        List<long> dueIds = [];
        for (var minutesOverdue = 5; minutesOverdue >= 1; minutesOverdue--)
        {
            dueIds.Add(await SeedAsync(
                context,
                cancellationToken,
                status: minutesOverdue % 2 == 0 ? JobStatus.Retrying : JobStatus.Pending,
                nextAttemptAt: Now.AddMinutes(-minutesOverdue),
                // Alternating, so the earliest three cannot all come from one tenant.
                tenantId: minutesOverdue % 2 == 0 ? SecondTenantId : FirstTenantId));
        }

        var notDueId = await SeedAsync(
            context, cancellationToken, status: JobStatus.Retrying, nextAttemptAt: Now.AddSeconds(60));
        var processingId = await SeedAsync(context, cancellationToken, status: JobStatus.Processing);
        var completedId = await SeedAsync(context, cancellationToken, status: JobStatus.Completed);
        var repository = new BackgroundJobStateRepository(context);

        var before = await SnapshotAsync(context, cancellationToken);

        // Ambient tenant 0 is what lets the sweeper see every tenant's jobs.
        context.GetTenantId().Should().Be(0);

        // Act
        var eligible = await repository.FindEligibleAsync(RegisteredJobTypes, Now, 3, cancellationToken);
        var withoutRegisteredTypes = await repository.FindEligibleAsync([], Now, 3, cancellationToken);
        var beyondTheLimit = await repository.FindEligibleAsync(RegisteredJobTypes, Now, 200, cancellationToken);

        // Assert
        eligible.Select(item => item.JobId).Should().Equal(dueIds.Take(3));
        eligible.Select(item => item.JobId).Should().BeInAscendingOrder();
        eligible.Should().AllSatisfy(item => item.JobType.Should().Be(KnownJobType));

        var tenants = await context.BackgroundJobs
            .AsNoTracking()
            .Where(job => eligible.Select(item => item.JobId).Contains(job.Id))
            .Select(job => job.TenantId)
            .Distinct()
            .ToListAsync(cancellationToken);
        tenants.Should().BeEquivalentTo([FirstTenantId, SecondTenantId]);

        (await SnapshotAsync(context, cancellationToken)).Should().BeEquivalentTo(before);

        withoutRegisteredTypes.Should().BeEmpty();

        // Past the limit, because the three earliest due rows sort ahead of the ineligible ones.
        var beyondTheLimitIds = beyondTheLimit.Select(item => item.JobId).ToList();
        beyondTheLimitIds.Should().Equal(dueIds);
        beyondTheLimitIds.Should().NotContain([notDueId, processingId, completedId]);
    }

    [Fact]
    public async Task FindStaleAsync_MixedHeartbeats_ReturnsOldestStale()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = fixture.Factory.Services.CreateScope();
        var context = JobsContext(scope);
        await ClearJobsAsync(context, cancellationToken);
        var longestSilentId = await SeedAsync(
            context, cancellationToken, status: JobStatus.Processing, heartbeatAt: Now.AddMinutes(-15));
        var staleId = await SeedAsync(
            context, cancellationToken, status: JobStatus.Processing, heartbeatAt: Now.AddMinutes(-11));
        await SeedAsync(
            context, cancellationToken, status: JobStatus.Processing, heartbeatAt: Now.AddMinutes(-1));
        var repository = new BackgroundJobStateRepository(context);

        // Act
        var stale = await repository.FindStaleAsync(Now.AddMinutes(-10), 200, cancellationToken);

        // Assert
        stale.Select(job => job.Id).Should().Equal(longestSilentId, staleId);
        stale.Should().AllSatisfy(job => job.JobType.Should().Be(KnownJobType));
    }

    [Fact]
    public async Task FindStaleAsync_NullHeartbeat_ReturnsFirstAndReaps()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        using var scope = fixture.Factory.Services.CreateScope();
        var context = JobsContext(scope);
        await ClearJobsAsync(context, cancellationToken);
        var staleCutoff = Now.AddMinutes(-10);
        var longestSilentId = await SeedAsync(
            context, cancellationToken, status: JobStatus.Processing, attemptCount: 1, heartbeatAt: Now.AddMinutes(-15));
        var noHeartbeatId = await SeedAsync(
            context, cancellationToken, status: JobStatus.Processing, attemptCount: 1, heartbeatAt: null);
        await SeedAsync(
            context, cancellationToken, status: JobStatus.Processing, attemptCount: 1, heartbeatAt: Now.AddMinutes(-1));
        var repository = new BackgroundJobStateRepository(context);

        // Act
        var stale = await repository.FindStaleAsync(staleCutoff, 200, cancellationToken);
        var reaped = await repository.TryReapAsync(
            noHeartbeatId, 1, staleCutoff, 3, Now.AddSeconds(30), ReapedMessage, Now, cancellationToken);

        // Assert — nothing proves a running job without a heartbeat alive.
        stale.Select(job => job.Id).Should().Equal(noHeartbeatId, longestSilentId);
        reaped.Should().BeTrue();
        var job = await ReadAsync(context, noHeartbeatId, cancellationToken);
        job.Status.Should().Be(JobStatus.Retrying);
        job.ErrorMessage.Should().Be(ReapedMessage);
        job.AttemptCount.Should().Be(1);
    }

    private static JobsPostgreSqlDbContext JobsContext(IServiceScope scope) =>
        scope.ServiceProvider.GetRequiredService<JobsPostgreSqlDbContext>();

    // Ambient tenant 0 turns the tenant filter off, so this clears every tenant's rows.
    private static Task ClearJobsAsync(JobsPostgreSqlDbContext context, CancellationToken cancellationToken) =>
        context.BackgroundJobs.ExecuteDeleteAsync(cancellationToken);

    // Columns are set directly: the entity's guarded transitions cannot reach every state a fence is tried against.
    private static async Task<long> SeedAsync(
        JobsPostgreSqlDbContext context,
        CancellationToken cancellationToken,
        JobStatus status = JobStatus.Pending,
        DateTime? nextAttemptAt = null,
        string jobType = KnownJobType,
        long tenantId = FirstTenantId,
        int attemptCount = 0,
        DateTime? startedAt = null,
        DateTime? heartbeatAt = null,
        DateTime? completedAt = null,
        string? errorMessage = null)
    {
        var job = new BackgroundJob(jobType, PayloadJson, tenantId, nextAttemptAt ?? Now);
        context.BackgroundJobs.Add(job);
        await context.SaveChangesAsync(cancellationToken);

        await context.BackgroundJobs
            .Where(row => row.Id == job.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(row => row.Status, status)
                    .SetProperty(row => row.AttemptCount, attemptCount)
                    .SetProperty(row => row.StartedAt, startedAt)
                    .SetProperty(row => row.HeartbeatAt, heartbeatAt)
                    .SetProperty(row => row.CompletedAt, completedAt)
                    .SetProperty(row => row.ErrorMessage, errorMessage)
                    .SetProperty(row => row.ModifiedAt, (DateTime?)SeededModifiedAt),
                cancellationToken);

        return job.Id;
    }

    private static Task SetStatusAsync(
        JobsPostgreSqlDbContext context,
        long jobId,
        JobStatus status,
        CancellationToken cancellationToken) =>
        context.BackgroundJobs
            .Where(row => row.Id == jobId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(row => row.Status, status), cancellationToken);

    private static Task<BackgroundJob> ReadAsync(
        JobsPostgreSqlDbContext context,
        long jobId,
        CancellationToken cancellationToken) =>
        context.BackgroundJobs.AsNoTracking().SingleAsync(job => job.Id == jobId, cancellationToken);

    private static async Task<List<(long Id, JobStatus Status, DateTime? ModifiedAt)>> SnapshotAsync(
        JobsPostgreSqlDbContext context,
        CancellationToken cancellationToken) =>
        await context.BackgroundJobs
            .AsNoTracking()
            .OrderBy(job => job.Id)
            .Select(job => new ValueTuple<long, JobStatus, DateTime?>(job.Id, job.Status, job.ModifiedAt))
            .ToListAsync(cancellationToken);

    // Polls from a connection outside any transaction, because pg_stat_activity is a per-transaction snapshot.
    private static async Task WaitForUpdateBlockedByAsync(
        JobsPostgreSqlDbContext context,
        int lockHolderPid,
        Task claim,
        CancellationToken cancellationToken)
    {
        var waited = Stopwatch.StartNew();
        while (true)
        {
            var blockedUpdates = await context.Database
                .SqlQuery<int>(
                    $"""
                    SELECT count(*)::int AS "Value"
                    FROM pg_stat_activity
                    WHERE {lockHolderPid} = ANY(pg_blocking_pids(pid)) AND query LIKE 'UPDATE%'
                    """)
                .SingleAsync(cancellationToken);

            if (blockedUpdates > 0)
            {
                return;
            }

            claim.IsCompleted.Should().BeFalse("the claim's update has to wait on the row lock");
            waited.Elapsed.Should().BeLessThan(LockWaitTimeout, "the claim's update never waited on the row lock");
            await Task.Delay(TimeSpan.FromMilliseconds(20), cancellationToken);
        }
    }
}
