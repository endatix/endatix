using System.Data;
using System.Globalization;
using System.Text.RegularExpressions;
using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.BackgroundJobs;
using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Domain;
using Endatix.Modules.Jobs.Features;
using Endatix.Modules.Jobs.Persistence;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using Quartz;
using Quartz.Impl.AdoJobStore;

namespace Endatix.IntegrationTests;

/// <summary>
/// The Background Jobs store on SQL Server, at database and scheduler level: what the SQL Server migrations create
/// and remove, how the dedup index they create compares keys, whether the scheduler accepts the tables, and which
/// triggers the driver delegate acquires.
/// Each test migrates a database of its own, so only that migration shapes it.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "SqlServer")]
public sealed partial class JobsSqlServerStoreTests(DbIntegrationFixture fixture)
{
    private const string SkipReason = "Asserts SQL Server catalog views and T-SQL.";

    // The trigger of group A that the acquisition-cost test copies into a backlog.
    private const string BacklogTemplate = "a0";

    // Copies the template trigger, one millisecond apart, until group A holds @triggers triggers. The column list
    // comes from the catalog, so the copies keep every column Quartz's script creates.
    private const string GrowBacklogSql =
        """
        DECLARE @offset int = (SELECT count(*) FROM jobs.qrtz_TRIGGERS WHERE JOB_NAME = N'A');
        DECLARE @columns nvarchar(max) = (
            SELECT STRING_AGG(CAST(QUOTENAME(name) AS nvarchar(max)), N', ') WITHIN GROUP (ORDER BY column_id)
            FROM sys.columns
            WHERE object_id = OBJECT_ID(N'jobs.qrtz_TRIGGERS') AND is_computed = 0
              AND name NOT IN (N'TRIGGER_NAME', N'NEXT_FIRE_TIME'));
        DECLARE @insert nvarchar(max) =
            N'INSERT INTO jobs.qrtz_TRIGGERS (TRIGGER_NAME, NEXT_FIRE_TIME, ' + @columns + N') ' +
            N'SELECT CONCAT(t.TRIGGER_NAME, N''-'', copies.n), t.NEXT_FIRE_TIME + copies.n * 10000, ' + @columns + N' ' +
            N'FROM jobs.qrtz_TRIGGERS AS t ' +
            N'CROSS JOIN (SELECT TOP (@count) @offset + ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) AS n ' +
            N'FROM sys.all_columns AS a CROSS JOIN sys.all_columns AS b) AS copies ' +
            N'WHERE t.TRIGGER_NAME = @template';
        DECLARE @count int = @triggers - @offset;
        EXEC sp_executesql @insert, N'@count int, @offset int, @template nvarchar(200)', @count, @offset, @template;
        """;

    private const string TablesInJobsSchema =
        "SELECT count(*) FROM sys.tables WHERE schema_id = SCHEMA_ID('jobs')";

    private const string IndexesInJobsSchema =
        """
        SELECT count(*) FROM sys.indexes i JOIN sys.tables t ON t.object_id = i.object_id
        WHERE t.schema_id = SCHEMA_ID('jobs') AND i.type > 0
        """;

    [Fact]
    public async Task JobsMigrations_FreshSqlServerDatabase_CreatesJobsAndQuartzTables()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.SqlServer, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(TestDatabaseProvider.SqlServer, fixture.ConnectionString, ct);
        await using var context = ContextFor(database);

        // Act
        await context.Database.MigrateAsync(ct);

        // Assert
        var tables = await database.QueryAsync(
            "SELECT name FROM sys.tables WHERE schema_id = SCHEMA_ID('jobs')", reader => reader.GetString(0), ct);
        var checkConstraints = await database.QueryAsync(
            "SELECT name, definition FROM sys.check_constraints WHERE parent_object_id = OBJECT_ID('jobs.BackgroundJobs')",
            reader => (Name: reader.GetString(0), Definition: reader.GetString(1)),
            ct);
        var jobIndexes = await database.QueryAsync(
            """
            SELECT name, is_unique, filter_definition FROM sys.indexes
            WHERE object_id = OBJECT_ID('jobs.BackgroundJobs') AND name LIKE 'IX[_]%'
            ORDER BY name
            """,
            reader => (Name: reader.GetString(0), IsUnique: reader.GetBoolean(1), Filter: reader.IsDBNull(2) ? null : reader.GetString(2)),
            ct);
        var appliedMigrations = await database.CountAsync("SELECT count(*) FROM jobs.__EFMigrationsHistory", ct);

        tables.Should().Contain(["BackgroundJobs", "__EFMigrationsHistory"]);
        tables.Should().Contain(QuartzSchemaScripts.SqlServerTableNames());
        checkConstraints.Should().ContainSingle()
            .Which.Should().Be(("CK_BackgroundJobs_TenantId", "([TenantId]>(0))"));
        jobIndexes.Should().Equal(
            ("IX_BackgroundJobs_DedupKey", true, "([DedupKey] IS NOT NULL)"),
            ("IX_BackgroundJobs_Expiry", false, null),
            ("IX_BackgroundJobs_Tenant", false, null));
        appliedMigrations.Should().Be(1);
        await ShouldHaveTheAcquisitionColumnAndIndexAsync(database, ct);
    }

    [Fact]
    public async Task QuartzSqlServerStatements_RunTwice_ChangeNothing()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.SqlServer, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(TestDatabaseProvider.SqlServer, fixture.ConnectionString, ct);
        await using (var context = ContextFor(database))
        {
            await context.Database.MigrateAsync(ct);
        }

        var tablesBefore = await database.CountAsync(TablesInJobsSchema, ct);
        var indexesBefore = await database.CountAsync(IndexesInJobsSchema, ct);

        // Act
        foreach (var statement in QuartzSchemaScripts.SqlServerTables())
        {
            await database.ExecuteAsync(statement, ct);
        }

        // Assert
        (await database.CountAsync(TablesInJobsSchema, ct)).Should().Be(tablesBefore);
        (await database.CountAsync(IndexesInJobsSchema, ct)).Should().Be(indexesBefore);
    }

    [Fact]
    public async Task JobsMigrations_SqlServerMigrateToZero_DropsJobsAndQuartzTables()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.SqlServer, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(TestDatabaseProvider.SqlServer, fixture.ConnectionString, ct);
        await using var context = ContextFor(database);
        await context.Database.MigrateAsync(ct);

        // Act
        await context.GetService<IMigrator>().MigrateAsync(Migration.InitialDatabase, cancellationToken: ct);

        // Assert
        var tables = await database.QueryAsync(
            "SELECT name FROM sys.tables WHERE schema_id = SCHEMA_ID('jobs')", reader => reader.GetString(0), ct);
        var appliedMigrations = await database.CountAsync("SELECT count(*) FROM jobs.__EFMigrationsHistory", ct);
        var acquisitionObjects = await database.CountAsync(
            """
            SELECT (SELECT count(*) FROM sys.columns WHERE name = 'ENDATIX_ACQUIRE_GROUP')
                 + (SELECT count(*) FROM sys.indexes WHERE name = 'idx_endatix_qrtz_t_acquire')
            """,
            ct);
        tables.Should().Equal("__EFMigrationsHistory");
        appliedMigrations.Should().Be(0);
        acquisitionObjects.Should().Be(0);
    }

    [Fact]
    public async Task EnqueueAsync_SqlServerDedupKeysDifferingOnlyInCase_AreDistinctJobsAndAnExactRepeatDedups()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.SqlServer, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await JobsTestDatabase.CreateAsync(TestDatabaseProvider.SqlServer, fixture.ConnectionString, ct);
        await using var context = ContextFor(database);
        await context.Database.MigrateAsync(ct);
        var queue = QueueOver(context);

        // Act
        var lowerCase = await queue.EnqueueAsync(ExportRequest("report"), ct);
        var upperCase = await queue.EnqueueAsync(ExportRequest("Report"), ct);
        var repeat = await queue.EnqueueAsync(ExportRequest("report"), ct);

        // Assert
        upperCase.Should().NotBe(lowerCase);
        repeat.Should().Be(lowerCase);
        (await database.CountAsync("SELECT count(*) FROM jobs.BackgroundJobs", ct)).Should().Be(2);
    }

    [Fact]
    public async Task QuartzValidation_SqlServerMigratedDatabase_Passes()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.SqlServer, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await MigratedDatabaseAsync(ct);
        await using var services = AcquisitionTestStore.SchedulerServices(
            AcquisitionTestStore.Configuration(TestDatabaseProvider.SqlServer, database.ConnectionString));

        // Act — building the scheduler validates its tables, the acquisition column among them.
        var scheduler = await AcquisitionTestStore.SchedulerOf(services, ct);

        // Assert
        scheduler.SchedulerName.Should().Be(QuartzRegistration.SchedulerName);
        await scheduler.Shutdown(cancellationToken: ct);
    }

    [Fact]
    public async Task QuartzValidation_SqlServerMissingTable_Fails()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.SqlServer, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var database = await MigratedDatabaseAsync(ct);
        await database.ExecuteAsync("DROP TABLE jobs.qrtz_LOCKS", ct);
        await using var services = AcquisitionTestStore.SchedulerServices(
            AcquisitionTestStore.Configuration(TestDatabaseProvider.SqlServer, database.ConnectionString));

        // Act
        var build = async () => await AcquisitionTestStore.SchedulerOf(services, ct);

        // Assert
        await build.Should().ThrowAsync<SchedulerException>().WithMessage("*schema validation failed*");
    }

    [Fact]
    public async Task SelectTriggersToAcquire_FullGroupAtHeadOfQueue_ReturnsOtherGroupsTrigger()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.SqlServer, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var store = await AcquisitionTestStore.CreateAsync(TestDatabaseProvider.SqlServer, fixture.ConnectionString, ct);
        var behind = await store.SeedHeadOfLineAsync(ct);

        // Act
        var acquired = await store.SelectTriggersToAcquireAsync(2, limits => limits.ForGroup("A", 0).ForGroup("B", 1), ct);

        // Assert — the same key the PostgreSQL delegate returns on this fixture.
        acquired.Should().Equal(behind);
    }

    [Fact]
    public async Task SelectTriggersToAcquire_GroupWithOneFreeSlot_ReturnsItsOldestTrigger()
    {
        // Arrange — b2 and b3 are due together; b3's priority is higher.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.SqlServer, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var store = await AcquisitionTestStore.CreateAsync(TestDatabaseProvider.SqlServer, fixture.ConnectionString, ct);
        AcquisitionTestTrigger[] waiting =
        [
            AcquisitionTestTrigger.Of("B", "b1", 5),
            AcquisitionTestTrigger.Of("B", "b2", 1),
            AcquisitionTestTrigger.Of("B", "b3", 1) with { Priority = 9 },
            AcquisitionTestTrigger.Of("B", "b4", 3),
            AcquisitionTestTrigger.Of("B", "b5", 4),
        ];
        foreach (var trigger in waiting)
        {
            await store.ScheduleAsync(trigger, ct);
        }

        // Act — a cap of two with one firing leaves one slot.
        var acquired = await store.SelectTriggersToAcquireAsync(10, limits => limits.ForGroup("B", 1), ct);

        // Assert
        acquired.Should().Equal(new TriggerKey("b3", "B"));
    }

    [Fact]
    public async Task SelectTriggersToAcquire_TriggerWithoutGroup_MatchesByJobName()
    {
        // Arrange — stored as Quartz stores the trigger that recovers a dead node's job: no execution group.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.SqlServer, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var store = await AcquisitionTestStore.CreateAsync(TestDatabaseProvider.SqlServer, fixture.ConnectionString, ct);
        var recovering = new AcquisitionTestTrigger(new TriggerKey("r1", "RECOVERING_JOBS"), "B", 0, ExecutionGroup: null);
        await store.ScheduleAsync(recovering, ct);

        // Act
        var acquired = await store.SelectTriggersToAcquireAsync(10, limits => limits.ForGroup("B", 1), ct);
        var read = await store.SelectTriggerAsync(recovering.Key, ct);

        // Assert
        acquired.Should().Equal(recovering.Key);
        read.ExecutionGroup.Should().Be("B");
    }

    [Fact]
    public async Task SelectTriggersToAcquire_LargeBacklogInFullGroup_ReadsStayFlat()
    {
        // Arrange — group A's backlog is due before B's only trigger, so a read that walked the queue in fire order
        // would read A's whole backlog first.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.SqlServer, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var store = await AcquisitionTestStore.CreateAsync(TestDatabaseProvider.SqlServer, fixture.ConnectionString, ct);
        await store.ScheduleAsync(AcquisitionTestTrigger.Of("A", BacklogTemplate, 0), ct);
        var behind = AcquisitionTestTrigger.Of("B", "b1", 1500);
        await store.ScheduleAsync(behind, ct);

        // Act
        await GrowBacklogAsync(store.Database, 5_000, ct);
        var atFiveThousand = await AcquireWithReadsAsync(store.Database, ct);
        await GrowBacklogAsync(store.Database, 50_000, ct);
        var atFiftyThousand = await AcquireWithReadsAsync(store.Database, ct);

        // Assert
        atFiveThousand.Acquired.Should().Equal(behind.Key);
        atFiftyThousand.Acquired.Should().Equal(behind.Key);
        TestContext.Current.TestOutputHelper?.WriteLine(
            $"Logical reads on qrtz_TRIGGERS per acquisition: {atFiveThousand.Reads} at 5,000 waiting triggers in the " +
            $"full group, {atFiftyThousand.Reads} at 50,000.");
        atFiveThousand.Reads.Should().BePositive();
        atFiftyThousand.Reads.Should().BeLessThanOrEqualTo(2 * atFiveThousand.Reads).And.BeLessThanOrEqualTo(1_000);
    }

    // Grows group A's backlog in one statement run on the server, rather than a round trip per trigger.
    private static async Task GrowBacklogAsync(JobsTestDatabase database, int triggers, CancellationToken ct)
    {
        await using var connection = new SqlConnection(database.ConnectionString);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(GrowBacklogSql, connection) { CommandTimeout = 300 };
        command.Parameters.AddWithValue("@triggers", triggers);
        command.Parameters.AddWithValue("@template", BacklogTemplate);
        await command.ExecuteNonQueryAsync(ct);
    }

    // The SQL Server delegate's acquisition statement for a batch of 11, with A full and one slot free in B, and the
    // logical reads SQL Server reports on qrtz_TRIGGERS for it. It runs on a connection of the test's own, because
    // SQL Server sends the statistics after the rows and the client raises them only once a reader moves past the
    // rows, which the scheduler's reader never does.
    private static async Task<(List<TriggerKey> Acquired, long Reads)> AcquireWithReadsAsync(
        JobsTestDatabase database,
        CancellationToken ct)
    {
        await using var connection = new SqlConnection(database.ConnectionString);
        await connection.OpenAsync(ct);
        var reads = 0L;
        connection.InfoMessage += (_, message) => reads += TriggerReadsIn(message.Message);
        await using (var statistics = new SqlCommand("SET STATISTICS IO ON", connection))
        {
            await statistics.ExecuteNonQueryAsync(ct);
        }

        await using var command = AcquisitionCommand(connection, batchSize: 11, freeSlotsJson: """[{"g":"B","s":1}]""");
        List<TriggerKey> acquired = [];
        await using (var reader = await command.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                acquired.Add(new TriggerKey(
                    reader.GetString(reader.GetOrdinal("TRIGGER_NAME")),
                    reader.GetString(reader.GetOrdinal("TRIGGER_GROUP"))));
            }

            await reader.NextResultAsync(ct);
        }

        return (acquired, reads);
    }

    // The rewritten statement with the values the scheduler binds to it.
    private static SqlCommand AcquisitionCommand(SqlConnection connection, int batchSize, string freeSlotsJson)
    {
        var statement = ExecutionGroupFilteringSqlServerDelegate
            .LimitEachGroupToItsFreeSlots(new SqlServerDialect().AcquisitionSql(batchSize), batchSize)
            .Replace("{0}", QuartzRegistration.TablePrefix, StringComparison.Ordinal);
        var command = new SqlCommand(statement, connection);
        var now = DateTimeOffset.UtcNow;
        command.Parameters.AddWithValue("@schedulerName", QuartzRegistration.SchedulerName);
        command.Parameters.AddWithValue("@state", "WAITING");
        command.Parameters.AddWithValue("@noLaterThan", now.AddSeconds(30).UtcTicks);
        command.Parameters.AddWithValue("@noEarlierThan", now.AddDays(-1).UtcTicks);
        command.Parameters.AddWithValue("@instanceId", "acquisition-test");
        command.Parameters.AddWithValue("@autoPinSentinel", "*");
        command.Parameters.AddWithValue("@liveNodeCutoff", now.AddSeconds(-15).UtcTicks);
        command.Parameters.Add(new SqlParameter("@endatix_free_slots", SqlDbType.NVarChar, -1) { Value = freeSlotsJson });
        return command;
    }

    // The statement Quartz's SQL Server dialect builds for an acquisition without job-type exclusions.
    private sealed class SqlServerDialect : SqlServerDelegate
    {
        public string AcquisitionSql(int maxCount) =>
            GetSelectNextTriggerToAcquireSql(new TriggerAcquisitionSqlShape(maxCount, 0));
    }

    private static long TriggerReadsIn(string message) =>
        TriggerReads().Matches(message).Sum(match => long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture));

    [GeneratedRegex(@"Table 'qrtz_TRIGGERS'\. Scan count \d+, logical reads (\d+)")]
    private static partial Regex TriggerReads();

    private async Task<JobsTestDatabase> MigratedDatabaseAsync(CancellationToken ct)
    {
        var database = await JobsTestDatabase.CreateAsync(TestDatabaseProvider.SqlServer, fixture.ConnectionString, ct);
        try
        {
            await using var context = ContextFor(database);
            await context.Database.MigrateAsync(ct);
            return database;
        }
        catch
        {
            await database.DisposeAsync();
            throw;
        }
    }

    private static BackgroundJobRequest ExportRequest(string dedupKey) =>
        new("SubmissionExport", "{}", TenantId: 7, DedupKey: dedupKey);

    // The real queue over the SQL Server context; the scheduler only commits, since this asserts the rows alone.
    private static BackgroundJobQueue QueueOver(JobsSqlServerDbContext context)
    {
        var clock = Substitute.For<IDateTimeProvider>();
        clock.UtcNow.Returns(DateTimeOffset.UtcNow);
        var scheduler = Substitute.For<IJobTriggerScheduler>();
        scheduler.ScheduleAndCommitAsync(
                Arg.Any<IDbContextTransaction>(), Arg.Any<IReadOnlyList<BackgroundJob>>(), Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<IDbContextTransaction>().CommitAsync(call.Arg<CancellationToken>()));
        return new BackgroundJobQueue(context, clock, scheduler);
    }

    private static async Task ShouldHaveTheAcquisitionColumnAndIndexAsync(JobsTestDatabase database, CancellationToken ct)
    {
        var columns = await database.QueryAsync(
            """
            SELECT is_persisted, definition FROM sys.computed_columns
            WHERE object_id = OBJECT_ID('jobs.qrtz_TRIGGERS') AND name = 'ENDATIX_ACQUIRE_GROUP'
            """,
            reader => (IsPersisted: reader.GetBoolean(0), Definition: reader.GetString(1)),
            ct);
        var indexColumns = await database.QueryAsync(
            """
            SELECT c.name, ic.is_descending_key, ic.is_included_column
            FROM sys.indexes i
            JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
            JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
            WHERE i.object_id = OBJECT_ID('jobs.qrtz_TRIGGERS') AND i.name = 'idx_endatix_qrtz_t_acquire'
            ORDER BY ic.is_included_column, ic.key_ordinal, c.name
            """,
            reader => (Name: reader.GetString(0), Descending: reader.GetBoolean(1), Included: reader.GetBoolean(2)),
            ct);

        columns.Should().ContainSingle().Which.IsPersisted.Should().BeTrue();
        columns[0].Definition.Should().ContainEquivalentOf("coalesce([EXECUTION_GROUP],[JOB_NAME])");
        indexColumns.Where(column => !column.Included).Should().Equal(
            ("SCHED_NAME", false, false),
            ("TRIGGER_STATE", false, false),
            ("ENDATIX_ACQUIRE_GROUP", false, false),
            ("NEXT_FIRE_TIME", false, false),
            ("PRIORITY", true, false));
        indexColumns.Where(column => column.Included).Select(column => column.Name).Should().BeEquivalentTo(
            "JOB_NAME", "JOB_GROUP", "EXECUTION_GROUP", "MISFIRE_INSTR", "PREFERRED_NODE");
    }

    // Built the way the module registers its context, so the test migrates with the runtime's options.
    private static JobsSqlServerDbContext ContextFor(JobsTestDatabase database)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = database.ConnectionString,
                ["ConnectionStrings:DefaultConnection_DbProvider"] = "sqlserver",
            })
            .Build();
        var options = new DbContextOptionsBuilder<JobsSqlServerDbContext>();
        options.ConfigureModuleDbContext(configuration, JobsPersistence.ConfigureDbContextOptions);
        return new JobsSqlServerDbContext(options.Options, DesignTimeDbContextDependencies.TenantContext);
    }
}
