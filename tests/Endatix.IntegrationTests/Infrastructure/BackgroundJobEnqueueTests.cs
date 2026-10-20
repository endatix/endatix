using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Endatix.IntegrationTests;

/// <summary>
/// Enqueueing writes the job rows and their scheduler triggers in one database transaction.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class BackgroundJobEnqueueTests(DbIntegrationFixture fixture)
{
    [Fact]
    public async Task EnqueueManyAsync_Commits_RowsAndTriggersTogether()
    {
        // Arrange — a schedule-only node, so nothing fires the triggers before they are counted.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, cancellationToken);
        await using var node = JobsTestNode.Create(
            database,
            new Dictionary<string, string?> { ["Endatix:BackgroundJobs:RunInProcess"] = "false" });
        await node.StartAsync(cancellationToken);
        var requests = Enumerable.Range(0, 3)
            .Select(_ => BackgroundJobRequest.Create(new ProbePayload(), tenantId: 5))
            .ToList();

        // Act
        var jobIds = await node.EnqueueManyAsync(requests, cancellationToken);

        // Assert
        var pendingRows = await database.QueryAsync(
            $"""SELECT "Id" FROM jobs."BackgroundJobs" WHERE "Status" = 0 AND "Id" IN ({jobIds.IdList()})""",
            reader => reader.GetInt64(0),
            cancellationToken);
        var triggerNames = await database.QueryAsync(
            "SELECT TRIGGER_NAME FROM jobs.qrtz_TRIGGERS WHERE TRIGGER_GROUP <> 'endatix-maintenance'",
            reader => reader.GetString(0),
            cancellationToken);
        pendingRows.Should().BeEquivalentTo(jobIds);
        triggerNames.Should().BeEquivalentTo(jobIds.Select(id => id.ToString()));
    }

    [Fact]
    public async Task EnqueueManyAsync_FailureWhileScheduling_LeavesNeitherRowsNorTriggers()
    {
        // Arrange — the database refuses the second trigger any one transaction inserts, which is a failure
        // after the rows are already written.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, cancellationToken);
        await using var node = JobsTestNode.Create(
            database,
            new Dictionary<string, string?> { ["Endatix:BackgroundJobs:RunInProcess"] = "false" });
        await node.StartAsync(cancellationToken);
        await database.ExecuteAsync(RefuseSecondTriggerOfATransaction(database.Provider), cancellationToken);
        var requests = Enumerable.Range(0, 3)
            .Select(_ => BackgroundJobRequest.Create(new ProbePayload(), tenantId: 5))
            .ToList();

        // Act
        var enqueue = () => node.EnqueueManyAsync(requests, cancellationToken);

        // Assert
        await enqueue.Should().ThrowAsync<Exception>();
        var rows = await database.CountAsync("""SELECT count(*) FROM jobs."BackgroundJobs" """, cancellationToken);
        var triggers = await database.CountAsync("SELECT count(*) FROM jobs.qrtz_TRIGGERS WHERE TRIGGER_GROUP <> 'endatix-maintenance'", cancellationToken);
        rows.Should().Be(0);
        triggers.Should().Be(0);
    }

    // Each provider tells a trigger the current transaction inserted in its own way: PostgreSQL by the row's xmin,
    // SQL Server by a session value naming the transaction that inserted the last one.
    private static string RefuseSecondTriggerOfATransaction(TestDatabaseProvider provider) => provider switch
    {
        TestDatabaseProvider.PostgreSql =>
            """
            CREATE FUNCTION jobs.refuse_second_trigger() RETURNS trigger AS $$
            BEGIN
                IF EXISTS (SELECT 1 FROM jobs.qrtz_triggers WHERE xmin = pg_current_xact_id()::xid) THEN
                    RAISE EXCEPTION 'injected failure while scheduling the second trigger';
                END IF;
                RETURN NEW;
            END $$ LANGUAGE plpgsql;
            CREATE TRIGGER refuse_second_trigger BEFORE INSERT ON jobs.qrtz_triggers
                FOR EACH ROW EXECUTE FUNCTION jobs.refuse_second_trigger();
            """,
        TestDatabaseProvider.SqlServer =>
            """
            CREATE TRIGGER jobs.refuse_second_trigger ON jobs.qrtz_TRIGGERS AFTER INSERT AS
            BEGIN
                SET NOCOUNT ON;
                DECLARE @transaction bigint = CURRENT_TRANSACTION_ID();
                IF CAST(SESSION_CONTEXT(N'endatix_trigger_transaction') AS bigint) = @transaction
                    THROW 50000, 'injected failure while scheduling the second trigger', 1;
                EXEC sp_set_session_context N'endatix_trigger_transaction', @transaction;
            END
            """,
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported test database provider."),
    };

    [Fact]
    public async Task EnqueueManyAsync_DeadlockWhileStoringTriggers_RetriesOnceAndStoresEachJobOnce()
    {
        // Arrange — the third trigger store loses a deadlock, after the rows and two triggers are written in the same
        // transaction, so the rollback has both to take back.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, cancellationToken);
        var failures = TriggerStoreFailures.Deadlocked(stores: 1, after: 2);
        await using var node = JobsTestNode.Create(
            database,
            new Dictionary<string, string?> { ["Endatix:BackgroundJobs:RunInProcess"] = "false" },
            services => FailingTriggerStoreDelegate.Register(services, database.Provider, failures));
        await node.StartAsync(cancellationToken);
        var requests = Enumerable.Range(0, 3)
            .Select(key => BackgroundJobRequest.Create(new ProbePayload(), tenantId: 5, dedupKey: $"deadlock:{key}"))
            .ToList();

        // Act
        var jobIds = await node.EnqueueManyAsync(requests, cancellationToken);

        // Assert — the first attempt rolled back, rows and triggers alike, and the retry stored each job once.
        failures.Remaining.Should().Be(0);
        var rows = await database.QueryAsync(
            """SELECT "Id" FROM jobs."BackgroundJobs" """, reader => reader.GetInt64(0), cancellationToken);
        var triggerNames = await database.QueryAsync(
            "SELECT TRIGGER_NAME FROM jobs.qrtz_TRIGGERS WHERE TRIGGER_GROUP <> 'endatix-maintenance'",
            reader => reader.GetString(0),
            cancellationToken);
        rows.Should().BeEquivalentTo(jobIds);
        triggerNames.Should().BeEquivalentTo(jobIds.Select(id => id.ToString()));
    }

    [Fact]
    public async Task EnqueueManyAsync_RealDeadlockWhileStoringTriggers_RetriesOnceAndStoresEachJobOnce()
    {
        // Arrange — the third trigger store deadlocks with a transaction of the test's own, after two triggers were
        // written, and the database picks the enqueue as the victim: SQL Server has rolled its transaction back by
        // the time the enqueue hears of it, PostgreSQL has aborted it.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, cancellationToken);
        await using var deadlock = await ArrangedDeadlock.PrepareAsync(database, cancellationToken);
        var failures = TriggerStoreFailures.DeadlockedByTheDatabase(after: 2, deadlock.DeadlockAsync);
        await using var node = JobsTestNode.Create(
            database,
            new Dictionary<string, string?> { ["Endatix:BackgroundJobs:RunInProcess"] = "false" },
            services => FailingTriggerStoreDelegate.Register(services, database.Provider, failures));
        await node.StartAsync(cancellationToken);
        var requests = Enumerable.Range(0, 3)
            .Select(key => BackgroundJobRequest.Create(new ProbePayload(), tenantId: 5, dedupKey: $"real-deadlock:{key}"))
            .ToList();

        // Act
        var jobIds = await node.EnqueueManyAsync(requests, cancellationToken);

        // Assert — the database itself chose the enqueue, and the retry stored each job, and only its own, once.
        deadlock.Victim.Should().NotBeNull();
        DatabaseFailures.IsLostRace(deadlock.Victim).Should().BeTrue();
        var rows = await database.QueryAsync(
            """SELECT "Id" FROM jobs."BackgroundJobs" """, reader => reader.GetInt64(0), cancellationToken);
        var triggerNames = await database.QueryAsync(
            "SELECT TRIGGER_NAME FROM jobs.qrtz_TRIGGERS WHERE TRIGGER_GROUP <> 'endatix-maintenance'",
            reader => reader.GetString(0),
            cancellationToken);
        rows.Should().BeEquivalentTo(jobIds);
        triggerNames.Should().BeEquivalentTo(jobIds.Select(id => id.ToString()));
    }

    [Fact]
    public async Task Enqueue_stores_a_durable_job_again_when_it_was_deleted_after_an_earlier_enqueue()
    {
        // Arrange — the first enqueue stores the job type's durable job and the node remembers it; then it is deleted
        // behind the node's back, with the trigger that pointed at it.
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, cancellationToken);
        await using var node = JobsTestNode.Create(
            database,
            new Dictionary<string, string?> { ["Endatix:BackgroundJobs:RunInProcess"] = "false" });
        await node.StartAsync(cancellationToken);
        await node.EnqueueAsync(BackgroundJobRequest.Create(new ProbePayload(), tenantId: 5), cancellationToken);
        var scheduler = await node.Services
            .GetRequiredKeyedService<ISchedulerFactory>(QuartzRegistration.SchedulerName)
            .GetScheduler(cancellationToken);
        await scheduler.DeleteJob(QuartzRegistration.JobKeyFor(ProbePayload.JobType), cancellationToken);

        // Act
        var jobId = await node.EnqueueAsync(BackgroundJobRequest.Create(new ProbePayload(), tenantId: 5), cancellationToken);

        // Assert
        var durableJobs = await database.CountAsync(
            $"SELECT count(*) FROM jobs.qrtz_JOB_DETAILS WHERE JOB_NAME = '{ProbePayload.JobType}'", cancellationToken);
        var triggerNames = await database.QueryAsync(
            "SELECT TRIGGER_NAME FROM jobs.qrtz_TRIGGERS WHERE TRIGGER_GROUP <> 'endatix-maintenance'",
            reader => reader.GetString(0),
            cancellationToken);
        durableJobs.Should().Be(1);
        triggerNames.Should().Equal(jobId.ToString());
    }

    [Fact]
    public async Task Enqueued_job_executes_exactly_once()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture, cancellationToken);
        var invocations = new ProbeInvocations();
        await using var node = JobsTestNode.Create(
            database,
            configureServices: services => services.AddProbe(invocations));
        await node.StartAsync(cancellationToken);

        // Act
        var jobId = await node.EnqueueAsync(BackgroundJobRequest.Create(new ProbePayload(), tenantId: 5), cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);

        // Assert
        invocations.CountFor(jobId).Should().Be(1);
    }
}
