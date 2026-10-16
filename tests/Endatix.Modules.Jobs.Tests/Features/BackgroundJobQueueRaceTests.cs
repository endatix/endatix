using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Modules.Jobs.Features;
using Endatix.Modules.Jobs.Runtime;
using Endatix.Modules.Jobs.Tests.Shared;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace Endatix.Modules.Jobs.Tests.Features;

/// <summary>
/// What the queue does where the database decides: keys SQL Server cannot tell apart, and an insert that loses a
/// deadlock to another enqueue.
/// </summary>
public sealed class BackgroundJobQueueRaceTests
{
    private static readonly DateTime Now = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

    private readonly IJobTriggerScheduler _triggerScheduler = Substitute.For<IJobTriggerScheduler>();

    [Fact]
    public async Task EnqueueAsync_SqlServerKeyEndingInASpace_ThrowsBeforeTouchingTheDatabase()
    {
        // Arrange — nothing listens there, so any query would fail with something else.
        await using var context = SqlServerContext();
        var queue = QueueOver(context);

        // Act
        var enqueue = () => queue.EnqueueAsync(Keyed("Survey "), TestContext.Current.CancellationToken);

        // Assert
        await enqueue.Should().ThrowAsync<ArgumentException>().WithMessage("A dedup key ends with a space*");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task EnqueueManyAsync_BatchWithANullRequest_ThrowsArgumentNullOnEitherDatabase(bool sqlServer)
    {
        // Arrange
        await using var context = sqlServer ? SqlServerContext() : new TestJobsDbContext(TestJobsDbContext.InMemoryOptions(), new FixedTenantContext(0));
        var queue = QueueOver(context);

        // Act
        var enqueue = () => queue.EnqueueManyAsync([Keyed("k1"), null!], TestContext.Current.CancellationToken);

        // Assert
        await enqueue.Should().ThrowAsync<ArgumentNullException>().WithParameterName("requests");
    }

    [Fact]
    public async Task EnqueueAsync_KeyEndingInASpaceOnADatabaseThatComparesAsWritten_KeepsTheKey()
    {
        // Arrange
        await using var context = new TestJobsDbContext(TestJobsDbContext.InMemoryOptions(), new FixedTenantContext(0));
        var queue = QueueOver(context);

        // Act
        await queue.EnqueueAsync(Keyed("Survey "), TestContext.Current.CancellationToken);

        // Assert
        (await context.BackgroundJobs.SingleAsync(TestContext.Current.CancellationToken)).DedupKey.Should().Be("Survey ");
    }

    [Theory]
    [MemberData(nameof(Deadlocks))]
    public async Task EnqueueAsync_InsertDeadlocksOnce_RetriesAndReturnsTheJob(Exception deadlock)
    {
        // Arrange
        await using var context = new TestJobsDbContext(
            TestJobsDbContext.InMemoryOptions(new FailingSaves(deadlock, failures: 1)), new FixedTenantContext(0));
        var queue = QueueOver(context);

        // Act
        var jobId = await queue.EnqueueAsync(Keyed("k1"), TestContext.Current.CancellationToken);

        // Assert — the retry inserted and scheduled the job, once.
        (await context.BackgroundJobs.SingleAsync(TestContext.Current.CancellationToken)).Id.Should().Be(jobId);
        await _triggerScheduler.ReceivedWithAnyArgs(1).ScheduleAndCommitAsync(default!, default!, default);
    }

    [Theory]
    [MemberData(nameof(Deadlocks))]
    public async Task EnqueueAsync_InsertDeadlocksTwice_Throws(Exception deadlock)
    {
        // Arrange — a second deadlock in a row is not the race the retry is for.
        await using var context = new TestJobsDbContext(
            TestJobsDbContext.InMemoryOptions(new FailingSaves(deadlock, failures: 2)), new FixedTenantContext(0));
        var queue = QueueOver(context);

        // Act
        var enqueue = () => queue.EnqueueAsync(Keyed("k1"), TestContext.Current.CancellationToken);

        // Assert
        await enqueue.Should().ThrowAsync<DbUpdateException>();
        await _triggerScheduler.DidNotReceiveWithAnyArgs().ScheduleAndCommitAsync(default!, default!, default);
    }

    public static TheoryData<Exception> Deadlocks() => new()
    {
        SqlServerExceptions.WithNumbers(1205),
        new PostgresException("deadlock detected", "ERROR", "ERROR", PostgresErrorCodes.DeadlockDetected),
        new PostgresException("could not serialize access", "ERROR", "ERROR", PostgresErrorCodes.SerializationFailure),
    };

    // A SQL Server context over a server that is not there, so any query would fail.
    private static TestJobsDbContext SqlServerContext() => new(
        new DbContextOptionsBuilder<TestJobsDbContext>()
            .UseSqlServer("Server=127.0.0.1,1;Database=not_connected;User Id=x;Password=x;Connect Timeout=1")
            .Options,
        new FixedTenantContext(0));

    private BackgroundJobQueue QueueOver(TestJobsDbContext context)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(new DateTimeOffset(Now));
        return new BackgroundJobQueue(context, clock, _triggerScheduler);
    }

    private static BackgroundJobRequest Keyed(string dedupKey) =>
        new("SubmissionExport", """{"formId":"1"}""", TenantId: 7, DedupKey: dedupKey);

    // Fails the first saves as the driver would report a lost deadlock, then lets saves through.
    private sealed class FailingSaves(Exception deadlock, int failures) : SaveChangesInterceptor
    {
        private int _remaining = failures;

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            _remaining-- > 0
                ? throw new DbUpdateException("The save failed.", deadlock)
                : base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
