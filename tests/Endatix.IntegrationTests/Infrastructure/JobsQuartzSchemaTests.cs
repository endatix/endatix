using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Quartz;

namespace Endatix.IntegrationTests;

/// <summary>
/// The scheduler's tables and roles against PostgreSQL. Each test runs its nodes on a database of its own, so
/// only the migrations those nodes apply shape it.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "PostgreSql")]
public sealed class JobsQuartzSchemaTests(DbIntegrationFixture fixture)
{
    private const string SkipReason =
        "Background jobs are PostgreSQL-only; the module is not registered on this provider.";

    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(60);

    [Fact]
    public async Task JobsMigrations_FreshDatabase_CreatesQuartzTablesOnlyInJobsSchema()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, cancellationToken);
        await using var node = JobsTestNode.Create(database.ConnectionString);

        // Act
        await node.StartAsync(cancellationToken);

        // Assert
        var jobsMigrations = await database.CountAsync(
            """SELECT count(*) FROM jobs."__EFMigrationsHistory" """, cancellationToken);
        var heartbeatColumns = await database.CountAsync(
            """
            SELECT count(*) FROM information_schema.columns
            WHERE table_schema = 'jobs' AND table_name = 'BackgroundJobs' AND column_name = 'HeartbeatAt'
            """,
            cancellationToken);
        var triggersTable = await database.CountAsync(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'jobs' AND table_name = 'qrtz_triggers'",
            cancellationToken);
        var schedulerTablesOutsideJobs = await database.CountAsync(
            "SELECT count(*) FROM information_schema.tables WHERE table_name LIKE 'qrtz%' AND table_schema <> 'jobs'",
            cancellationToken);
        var appMigrationTables = await database.CountAsync(
            """
            SELECT count(*) FROM information_schema.tables
            WHERE table_name = '__EFMigrationsHistory' AND table_schema <> 'jobs'
            """,
            cancellationToken);
        jobsMigrations.Should().Be(1);
        heartbeatColumns.Should().Be(0);
        triggersTable.Should().Be(1);
        schedulerTablesOutsideJobs.Should().Be(0);
        appMigrationTables.Should().Be(0);
    }

    [Fact]
    public async Task QuartzValidation_MissingTables_FailsStartup()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, cancellationToken);
        await using (var migrated = JobsTestNode.Create(database.ConnectionString))
        {
            await migrated.StartAsync(cancellationToken);
            await migrated.StopAsync(cancellationToken);
        }

        await database.ExecuteAsync("DROP TABLE jobs.qrtz_triggers CASCADE", cancellationToken);
        await using var node = JobsTestNode.Create(database.ConnectionString);

        // Act
        var start = () => node.StartAsync(cancellationToken);

        // Assert — the migrations already ran, so nothing recreates the table, and the scheduler refuses it.
        await start.Should().ThrowAsync<SchedulerException>();
        var triggersTable = await database.CountAsync(
            "SELECT count(*) FROM information_schema.tables WHERE table_schema = 'jobs' AND table_name = 'qrtz_triggers'",
            cancellationToken);
        triggersTable.Should().Be(0);
    }

    [Fact]
    public async Task Schedule_only_host_enqueues_but_never_executes()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.PostgreSql, SkipReason);
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(fixture.ConnectionString, cancellationToken);
        var invocations = new ProbeInvocations();
        await using var scheduleOnly = JobsTestNode.Create(
            database.ConnectionString,
            new Dictionary<string, string?> { ["Endatix:BackgroundJobs:RunInProcess"] = "false" },
            services => services.AddProbe(invocations));
        await using var worker = JobsTestNode.Create(
            database.ConnectionString,
            configureServices: services => services.AddProbe(invocations));
        await scheduleOnly.StartAsync(cancellationToken);

        // Act
        var jobIds = await EnqueueAsync(scheduleOnly, 3, cancellationToken);
        await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken);
        var pendingRows = await database.CountAsync(
            $"""SELECT count(*) FROM jobs."BackgroundJobs" WHERE "Status" = 0 AND "Id" IN ({string.Join(',', jobIds)})""",
            cancellationToken);
        var triggersWhileOnlyScheduling = await TriggerCountAsync(database, jobIds, cancellationToken);
        await worker.StartAsync(cancellationToken);
        var fired = await JobsTestWait.UntilAsync(
            async () => await TriggerCountAsync(database, jobIds, cancellationToken) == 0,
            StartupTimeout,
            cancellationToken);

        // Assert
        pendingRows.Should().Be(3);
        triggersWhileOnlyScheduling.Should().Be(3);
        fired.Should().BeTrue("the executing node fires every trigger the schedule-only node wrote");
    }

    private static async Task<List<long>> EnqueueAsync(JobsTestNode node, int count, CancellationToken cancellationToken)
    {
        var requests = Enumerable.Range(0, count)
            .Select(_ => BackgroundJobRequest.Create(new ProbePayload(), tenantId: 5))
            .ToList();
        return [.. await node.EnqueueManyAsync(requests, cancellationToken)];
    }

    private static Task<long> TriggerCountAsync(
        JobsTestDatabase database,
        IReadOnlyCollection<long> jobIds,
        CancellationToken cancellationToken) =>
        database.CountAsync(
            $"SELECT count(*) FROM jobs.qrtz_triggers WHERE trigger_name IN ({string.Join(',', jobIds.Select(id => $"'{id}'"))})",
            cancellationToken);
}
