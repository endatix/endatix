using System.Diagnostics;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Features;
using Endatix.Modules.Jobs.Persistence;
using Endatix.Modules.Jobs.Runtime;
using Endatix.Modules.Jobs.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using NSubstitute.ExceptionExtensions;

namespace Endatix.Modules.Jobs.Tests.Features;

/// <summary>
/// Enqueue behaviour against a real (in-memory) context, so the committed row shape is exercised
/// rather than asserted against a mock's recorded calls.
/// </summary>
public class BackgroundJobQueueTests : IDisposable
{
    private static readonly DateTime Now = new(2026, 8, 14, 12, 0, 0, DateTimeKind.Utc);

    private readonly TestJobsDbContext _dbContext;
    private readonly BackgroundJobQueue _queue;
    private readonly IJobTriggerScheduler _triggerScheduler = Substitute.For<IJobTriggerScheduler>();

    public BackgroundJobQueueTests()
    {
        _dbContext = new TestJobsDbContext(TestJobsDbContext.InMemoryOptions(), new FixedTenantContext(0));

        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTimeOffset(Now));
        clock.Now.Returns(new DateTimeOffset(Now));

        _queue = new BackgroundJobQueue(_dbContext, clock, _triggerScheduler);
    }

    public void Dispose() => _dbContext.Dispose();

    private static BackgroundJobRequest Request(string jobType = "SubmissionExport", long tenantId = 7) =>
        new(jobType, """{"formId":"1"}""", tenantId);

    private BackgroundJobQueue QueueWith(IJobsDbContext dbContext, IJobMetrics? metrics = null)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTimeOffset(Now));

        return new BackgroundJobQueue(dbContext, clock, _triggerScheduler, metrics);
    }

    [Fact]
    public async Task EnqueueAsync_ValidRequest_PersistsAnImmediatelyEligibleJob()
    {
        // Arrange
        var request = Request();

        // Act
        var jobId = await _queue.EnqueueAsync(request);

        // Assert
        var job = await _dbContext.BackgroundJobs.SingleAsync();
        job.Id.Should().Be(jobId).And.NotBe(0);
        job.Status.Should().Be(JobStatus.Pending);
        job.JobType.Should().Be("SubmissionExport");
        job.TenantId.Should().Be(7);
        job.PayloadJson.Should().Be("""{"formId":"1"}""");
        job.AttemptCount.Should().Be(0);
        // Eligible now: backoff only ever moves this forward, after a failed attempt.
        job.NextAttemptAt.Should().Be(Now);
    }

    [Fact]
    public async Task EnqueueAsync_SystemEnqueuedJob_HasNoCreatingUser()
    {
        // Arrange — webhook fan-out has no requesting user.
        var request = Request("WebHookDelivery");

        // Act
        await _queue.EnqueueAsync(request);

        // Assert
        var job = await _dbContext.BackgroundJobs.SingleAsync();
        job.CreatedByUserId.Should().BeNull();
    }

    [Fact]
    public async Task EnqueueAsync_WithinAnActivity_CapturesTheTraceId()
    {
        // Arrange — the trace has to be captured at enqueue; by execution time the request is gone.
        // A plain Activity is started directly rather than through an ActivitySource, because the
        // code under test reads Activity.Current and this needs no listener to be sampling.
        using var activity = new Activity("enqueue").Start();
        activity.Id.Should().NotBeNullOrEmpty();

        // Act
        await _queue.EnqueueAsync(Request());

        // Assert
        var job = await _dbContext.BackgroundJobs.SingleAsync();
        job.TraceId.Should().Be(activity.Id);
    }

    [Fact]
    public async Task EnqueueAsync_NoAmbientActivity_LeavesTraceIdNull()
    {
        // Arrange
        Activity.Current.Should().BeNull();

        // Act
        await _queue.EnqueueAsync(Request());

        // Assert
        var job = await _dbContext.BackgroundJobs.SingleAsync();
        job.TraceId.Should().BeNull();
    }

    [Fact]
    public async Task EnqueueManyAsync_FanOut_PersistsEveryJobAndReturnsIdsInOrder()
    {
        // Arrange — one job per webhook endpoint.
        var requests = new[]
        {
            new BackgroundJobRequest("WebHookDelivery", """{"endpoint":"a"}""", 7),
            new BackgroundJobRequest("WebHookDelivery", """{"endpoint":"b"}""", 7),
            new BackgroundJobRequest("WebHookDelivery", """{"endpoint":"c"}""", 7),
        };

        // Act
        var jobIds = await _queue.EnqueueManyAsync(requests);

        // Assert
        jobIds.Should().HaveCount(3).And.OnlyHaveUniqueItems();
        var jobs = await _dbContext.BackgroundJobs.OrderBy(job => job.Id).ToListAsync();
        jobs.Select(job => job.Id).Should().BeEquivalentTo(jobIds, options => options.WithStrictOrdering());
        jobs.Select(job => job.PayloadJson)
            .Should().BeEquivalentTo(requests.Select(request => request.PayloadJson));
    }

    [Fact]
    public async Task EnqueueManyAsync_SingleSaveChanges_CommitsTheBatchAtomically()
    {
        // Arrange
        var requests = Enumerable.Range(0, 5)
            .Select(index => new BackgroundJobRequest("WebHookDelivery", $$"""{"endpoint":"{{index}}"}""", 7))
            .ToArray();

        // Act
        await _queue.EnqueueManyAsync(requests);

        // Assert — a fan-out that partially committed would deliver to some endpoints and silently
        // drop the rest, so the batch must be one round trip.
        _dbContext.SaveChangesCallCount.Should().Be(1);
        (await _dbContext.BackgroundJobs.CountAsync()).Should().Be(5);
    }

    [Fact]
    public async Task EnqueueManyAsync_EmptyBatch_WritesNothing()
    {
        // Arrange
        // Act
        var jobIds = await _queue.EnqueueManyAsync([]);

        // Assert — an event with no configured endpoints must not cost a round trip.
        jobIds.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task EnqueueAsync_NullRequest_Throws()
    {
        // Arrange
        // Act
        var act = async () => await _queue.EnqueueAsync(null!);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task EnqueueManyAsync_Requests_SchedulesEveryInsertedRowInItsTransaction()
    {
        // Arrange
        IReadOnlyList<long>? scheduledIds = null;
        await _triggerScheduler.ScheduleAndCommitAsync(
            Arg.Any<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction>(),
            Arg.Do<IReadOnlyList<Endatix.Modules.Jobs.Domain.BackgroundJob>>(jobs => scheduledIds = jobs.Select(job => job.Id).ToList()),
            Arg.Any<CancellationToken>());

        // Act
        var jobIds = await _queue.EnqueueManyAsync(
            [Request("A"), Request("B")],
            TestContext.Current.CancellationToken);

        // Assert — the rows were saved before scheduling, so every trigger names a real id.
        scheduledIds.Should().Equal(jobIds);
        jobIds.Should().OnlyContain(id => id != 0);
    }

    [Fact]
    public async Task EnqueueAsync_SchedulingFails_Throws()
    {
        // Arrange
        _triggerScheduler
            .ScheduleAndCommitAsync(default!, default!, default)
            .ReturnsForAnyArgs(Task.FromException(new InvalidOperationException("Scheduling failed.")));
        var metrics = Substitute.For<IJobMetrics>();
        var queue = QueueWith(_dbContext, metrics);

        // Act
        var act = () => queue.EnqueueAsync(Request(), TestContext.Current.CancellationToken);

        // Assert — a job that could not be scheduled was not enqueued, so the caller must know.
        await act.Should().ThrowAsync<InvalidOperationException>();
        metrics.ReceivedCalls().Should().BeEmpty();
    }

    [Fact]
    public async Task EnqueueManyAsync_MetricsRegistered_RecordsEnqueuedPerJob()
    {
        // Arrange
        var metrics = Substitute.For<IJobMetrics>();
        var queue = QueueWith(_dbContext, metrics);

        // Act
        await queue.EnqueueManyAsync(
            [Request("A"), Request("B"), Request("A")],
            TestContext.Current.CancellationToken);

        // Assert
        metrics.Received(2).Record(JobLifecycleEvent.Enqueued, "A");
        metrics.Received(1).Record(JobLifecycleEvent.Enqueued, "B");
    }

    [Fact]
    public async Task EnqueueAsync_MetricsThrows_DoesNotSurface()
    {
        // Arrange
        var metrics = Substitute.For<IJobMetrics>();
        metrics
            .When(substitute => substitute.Record(Arg.Any<JobLifecycleEvent>(), Arg.Any<string>()))
            .Throw(new InvalidOperationException("Recording failed."));
        var queue = QueueWith(_dbContext, metrics);

        // Act
        var act = () => queue.EnqueueAsync(Request(), TestContext.Current.CancellationToken);

        // Assert — the rows are committed, so a failing sink must not change what the caller gets.
        var jobId = (await act.Should().NotThrowAsync()).Subject;
        (await _dbContext.BackgroundJobs.SingleAsync(TestContext.Current.CancellationToken)).Id.Should().Be(jobId);
    }

    [Fact]
    public async Task EnqueueAsync_SaveFails_RecordsNothing()
    {
        // Arrange
        var options = TestJobsDbContext.InMemoryOptions(new FailingSaveInterceptor());
        await using var failingContext = new TestJobsDbContext(options, new FixedTenantContext(0));
        var metrics = Substitute.For<IJobMetrics>();
        var queue = QueueWith(failingContext, metrics);

        // Act
        var act = () => queue.EnqueueAsync(Request(), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        metrics.ReceivedCalls().Should().BeEmpty();
    }

    private sealed class FailingSaveInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            throw new DbUpdateException("The save failed.");
    }
}
