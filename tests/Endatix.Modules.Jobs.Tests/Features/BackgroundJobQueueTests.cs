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

    public BackgroundJobQueueTests()
    {
        var options = new DbContextOptionsBuilder<TestJobsDbContext>()
            .UseInMemoryDatabase($"jobs-{Guid.NewGuid()}")
            .Options;

        _dbContext = new TestJobsDbContext(options, new FixedTenantContext(0));

        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTimeOffset(Now));
        clock.Now.Returns(new DateTimeOffset(Now));

        _queue = new BackgroundJobQueue(_dbContext, clock);
    }

    public void Dispose() => _dbContext.Dispose();

    private static BackgroundJobRequest Request(string jobType = "SubmissionExport", long tenantId = 7) =>
        new(jobType, """{"formId":"1"}""", tenantId);

    private static BackgroundJobQueue QueueWith(
        IJobsDbContext dbContext,
        IJobDispatchStrategy dispatchStrategy,
        IJobMetrics? metrics = null)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTimeOffset(Now));

        return new BackgroundJobQueue(dbContext, clock, dispatchStrategy, metrics);
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
    public async Task EnqueueManyAsync_StrategyRegistered_OffersCommittedIdsInOrder()
    {
        // Arrange
        var offers = new List<(JobDispatchItem Item, int SaveChangesCallCount)>();
        var dispatchStrategy = Substitute.For<IJobDispatchStrategy>();
        dispatchStrategy.TryOffer(Arg.Any<JobDispatchItem>()).Returns(call =>
        {
            offers.Add((call.Arg<JobDispatchItem>(), _dbContext.SaveChangesCallCount));
            return true;
        });
        var queue = QueueWith(_dbContext, dispatchStrategy);

        // Act
        var jobIds = await queue.EnqueueManyAsync(
            [Request("A"), Request("B"), Request("A")],
            TestContext.Current.CancellationToken);

        // Assert
        offers.Select(offer => offer.Item).Should().Equal(
            new JobDispatchItem(jobIds[0], "A"),
            new JobDispatchItem(jobIds[1], "B"),
            new JobDispatchItem(jobIds[2], "A"));
        // A runner handed an id before its row commits would find nothing to claim.
        offers[0].SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task EnqueueAsync_OfferRejected_ReturnsIdAndRecordsMetric()
    {
        // Arrange
        var dispatchStrategy = Substitute.For<IJobDispatchStrategy>();
        dispatchStrategy.TryOffer(Arg.Any<JobDispatchItem>()).Returns(false);
        var metrics = Substitute.For<IJobMetrics>();
        var queue = QueueWith(_dbContext, dispatchStrategy, metrics);

        // Act
        var jobId = await queue.EnqueueAsync(Request(), TestContext.Current.CancellationToken);

        // Assert
        var job = await _dbContext.BackgroundJobs.SingleAsync(TestContext.Current.CancellationToken);
        job.Id.Should().Be(jobId);
        metrics.Received(1).Record(JobLifecycleEvent.Enqueued, "SubmissionExport");
        metrics.Received(1).Record(JobLifecycleEvent.OfferRejected, "SubmissionExport");
    }

    [Fact]
    public async Task EnqueueAsync_StrategyThrows_DoesNotSurface()
    {
        // Arrange
        var dispatchStrategy = Substitute.For<IJobDispatchStrategy>();
        dispatchStrategy.TryOffer(Arg.Any<JobDispatchItem>()).Throws(new InvalidOperationException("Offer failed."));
        var queue = QueueWith(_dbContext, dispatchStrategy);

        // Act
        var act = () => queue.EnqueueAsync(Request(), TestContext.Current.CancellationToken);

        // Assert
        var jobId = (await act.Should().NotThrowAsync()).Subject;
        var job = await _dbContext.BackgroundJobs.SingleAsync(TestContext.Current.CancellationToken);
        job.Id.Should().Be(jobId);
        dispatchStrategy.Received(1).TryOffer(new JobDispatchItem(jobId, "SubmissionExport"));
    }

    [Fact]
    public async Task EnqueueManyAsync_FirstOfferThrows_StillOffersTheRestInOrder()
    {
        // Arrange
        var offers = new List<JobDispatchItem>();
        var dispatchStrategy = Substitute.For<IJobDispatchStrategy>();
        dispatchStrategy.TryOffer(Arg.Any<JobDispatchItem>()).Returns(call =>
        {
            offers.Add(call.Arg<JobDispatchItem>());
            return offers.Count == 1 ? throw new InvalidOperationException("Offer failed.") : true;
        });
        var queue = QueueWith(_dbContext, dispatchStrategy);

        // Act
        var act = () => queue.EnqueueManyAsync(
            [Request("A"), Request("B"), Request("C")],
            TestContext.Current.CancellationToken);

        // Assert
        var jobIds = (await act.Should().NotThrowAsync()).Subject;
        var committedIds = await _dbContext.BackgroundJobs
            .Select(job => job.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        jobIds.Should().HaveCount(3).And.BeEquivalentTo(committedIds);
        // One failed offer only leaves its own job to the sweep; the jobs after it are still signalled.
        offers.Should().Equal(
            new JobDispatchItem(jobIds[0], "A"),
            new JobDispatchItem(jobIds[1], "B"),
            new JobDispatchItem(jobIds[2], "C"));
    }

    [Fact]
    public async Task EnqueueAsync_MetricsThrows_DoesNotSurface()
    {
        // Arrange
        var dispatchStrategy = Substitute.For<IJobDispatchStrategy>();
        dispatchStrategy.TryOffer(Arg.Any<JobDispatchItem>()).Returns(false);
        var metrics = Substitute.For<IJobMetrics>();
        metrics
            .When(substitute => substitute.Record(Arg.Any<JobLifecycleEvent>(), Arg.Any<string>()))
            .Throw(new InvalidOperationException("Recording failed."));
        var queue = QueueWith(_dbContext, dispatchStrategy, metrics);

        // Act
        var act = () => queue.EnqueueAsync(Request(), TestContext.Current.CancellationToken);

        // Assert — the failed Enqueued record did not stop the rejection being recorded, and neither failure surfaced.
        var jobId = (await act.Should().NotThrowAsync()).Subject;
        dispatchStrategy.Received(1).TryOffer(new JobDispatchItem(jobId, "SubmissionExport"));
        metrics.Received(1).Record(JobLifecycleEvent.OfferRejected, "SubmissionExport");
    }

    [Fact]
    public async Task EnqueueManyAsync_StrategyAndMetricsRegistered_OffersEveryJobBeforeRecordingAnyMetric()
    {
        // Arrange — one log shared by both seams shows the order of their calls, whatever each returns.
        const string Offer = nameof(IJobDispatchStrategy.TryOffer);
        const string Metric = nameof(IJobMetrics.Record);
        var calls = new List<string>();
        var dispatchStrategy = Substitute.For<IJobDispatchStrategy>();
        dispatchStrategy.TryOffer(Arg.Any<JobDispatchItem>()).Returns(call =>
        {
            calls.Add(Offer);
            return call.Arg<JobDispatchItem>().JobType != "B";
        });
        var metrics = Substitute.For<IJobMetrics>();
        metrics
            .When(substitute => substitute.Record(Arg.Any<JobLifecycleEvent>(), Arg.Any<string>()))
            .Do(_ => calls.Add(Metric));
        var queue = QueueWith(_dbContext, dispatchStrategy, metrics);

        // Act
        await queue.EnqueueManyAsync(
            [Request("A"), Request("B"), Request("C")],
            TestContext.Current.CancellationToken);

        // Assert — a slow host-supplied metrics sink must not delay dispatch, so no offer waits behind a metric call.
        // The metric calls are three Enqueued and one OfferRejected, for the refused B.
        calls.Should().Equal(Offer, Offer, Offer, Metric, Metric, Metric, Metric);
        metrics.Received(3).Record(JobLifecycleEvent.Enqueued, Arg.Any<string>());
        metrics.Received(1).Record(JobLifecycleEvent.OfferRejected, "B");
    }

    [Fact]
    public async Task EnqueueAsync_SaveFails_OffersNothing()
    {
        // Arrange
        var options = new DbContextOptionsBuilder<TestJobsDbContext>()
            .UseInMemoryDatabase($"jobs-{Guid.NewGuid()}")
            .AddInterceptors(new FailingSaveInterceptor())
            .Options;
        await using var failingContext = new TestJobsDbContext(options, new FixedTenantContext(0));
        var dispatchStrategy = Substitute.For<IJobDispatchStrategy>();
        var metrics = Substitute.For<IJobMetrics>();
        var queue = QueueWith(failingContext, dispatchStrategy, metrics);

        // Act
        var act = () => queue.EnqueueAsync(Request(), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<DbUpdateException>();
        dispatchStrategy.DidNotReceiveWithAnyArgs().TryOffer(default);
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
