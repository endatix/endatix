using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>
/// Enqueueing writes the job rows and their scheduler triggers in one PostgreSQL transaction.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class BackgroundJobEnqueueTests(DbIntegrationFixture fixture)
{
    private const string SkipReason =
        "Background jobs are PostgreSQL-only; the module is not registered on this provider.";

    [Fact]
    public async Task EnqueueManyAsync_Commits_RowsAndTriggersTogether()
    {
        // Arrange — a schedule-only node, so nothing fires the triggers before they are counted.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, cancellationToken);
        await using var node = JobsTestNode.Create(
            database.ConnectionString,
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
            "SELECT trigger_name FROM jobs.qrtz_triggers",
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
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, cancellationToken);
        await using var node = JobsTestNode.Create(
            database.ConnectionString,
            new Dictionary<string, string?> { ["Endatix:BackgroundJobs:RunInProcess"] = "false" });
        await node.StartAsync(cancellationToken);
        await database.ExecuteAsync(
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
            cancellationToken);
        var requests = Enumerable.Range(0, 3)
            .Select(_ => BackgroundJobRequest.Create(new ProbePayload(), tenantId: 5))
            .ToList();

        // Act
        var enqueue = () => node.EnqueueManyAsync(requests, cancellationToken);

        // Assert
        await enqueue.Should().ThrowAsync<Exception>();
        var rows = await database.CountAsync("""SELECT count(*) FROM jobs."BackgroundJobs" """, cancellationToken);
        var triggers = await database.CountAsync("SELECT count(*) FROM jobs.qrtz_triggers", cancellationToken);
        rows.Should().Be(0);
        triggers.Should().Be(0);
    }

    [Fact]
    public async Task Enqueued_job_executes_exactly_once()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, cancellationToken);
        var invocations = new ProbeInvocations();
        await using var node = JobsTestNode.Create(
            database.ConnectionString,
            configureServices: services => services.AddProbe(invocations));
        await node.StartAsync(cancellationToken);

        // Act
        var jobId = await node.EnqueueAsync(BackgroundJobRequest.Create(new ProbePayload(), tenantId: 5), cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);

        // Assert
        invocations.CountFor(jobId).Should().Be(1);
    }
}
