using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;

namespace Endatix.IntegrationTests;

/// <summary>The fenced writes that end an attempt: each lands only on the attempt it names.</summary>
public sealed partial class BackgroundJobStateRepositoryIntegrationTests
{
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
        var completed = await repository.TryCompleteAsync(new AttemptRef(runningId, 2), FinishedNow, cancellationToken);
        var completedCanceled = await repository.TryCompleteAsync(
            new AttemptRef(canceledId, 2), FinishedNow, cancellationToken);

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
        var shortMessageId = await SeedAsync(context, cancellationToken, status: JobStatus.Processing, attemptCount: 1);
        var longMessageId = await SeedAsync(context, cancellationToken, status: JobStatus.Processing, attemptCount: 1);
        var repository = new BackgroundJobStateRepository(context);

        // Act
        var failed = await repository.TryFailAsync(
            new AttemptRef(shortMessageId, 1), FailureAt(FailureMessage), cancellationToken);
        var failedWithLongMessage = await repository.TryFailAsync(
            new AttemptRef(longMessageId, 1),
            new AttemptFailure(new string('a', 3000), Now, Retention),
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
        var retryingId = await SeedAsync(context, cancellationToken, status: JobStatus.Processing, attemptCount: 1);
        var exhaustedId = await SeedAsync(context, cancellationToken, status: JobStatus.Processing, attemptCount: 3);
        var repository = new BackgroundJobStateRepository(context);

        // Act
        var retried = await repository.RecordFailedAttemptAsync(
            new AttemptRef(retryingId, 1),
            new RetryableFailure(FailureAt(RetryMessage), 3, nextAttemptAt),
            cancellationToken);
        var deadLettered = await repository.RecordFailedAttemptAsync(
            new AttemptRef(exhaustedId, 3),
            new RetryableFailure(FailureAt(RetryMessage), 3, nextAttemptAt),
            cancellationToken);

        // Assert
        retried.Should().BeTrue();
        var retrying = await ReadAsync(context, retryingId, cancellationToken);
        retrying.Status.Should().Be(JobStatus.Retrying);
        retrying.NextAttemptAt.Should().Be(nextAttemptAt);
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
        var jobId = await SeedAsync(context, cancellationToken, status: JobStatus.Processing, attemptCount: 1);
        var repository = new BackgroundJobStateRepository(context);

        // Act
        var retried = await repository.RecordFailedAttemptAsync(
            new AttemptRef(jobId, 1), Retryable(new AttemptFailure(new string('a', 3000), Now, Retention)), cancellationToken);

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
        var jobId = await SeedAsync(context, cancellationToken, status: JobStatus.Processing, attemptCount: 1);
        var before = await ReadAsync(context, jobId, cancellationToken);
        var repository = new BackgroundJobStateRepository(context);

        // Act
        var fail = () => repository.TryFailAsync(new AttemptRef(jobId, 1), FailureAt(errorMessage!), cancellationToken);
        var recordFailedAttempt = () => repository.RecordFailedAttemptAsync(
            new AttemptRef(jobId, 1), Retryable(FailureAt(errorMessage!)), cancellationToken);

        // Assert — the entity's own failure transitions refuse such a message too.
        await fail.Should().ThrowAsync<ArgumentException>();
        await recordFailedAttempt.Should().ThrowAsync<ArgumentException>();
        var job = await ReadAsync(context, jobId, cancellationToken);
        job.Status.Should().Be(before.Status);
        job.ErrorMessage.Should().Be(before.ErrorMessage);
        job.CompletedAt.Should().Be(before.CompletedAt);
    }

    private static AttemptFailure FailureAt(string message) => new(message, Now, Retention);

    private static RetryableFailure Retryable(AttemptFailure failure) => new(failure, 3, Now.AddSeconds(30));
}
