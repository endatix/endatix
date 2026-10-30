using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Persistence;
using Endatix.Modules.Jobs.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Quartz;

namespace Endatix.IntegrationTests;

/// <summary>
/// The jobs schema as the startup migrations leave it. Unit tests pin what the entity exposes; only a
/// migrated database can show that the table agrees with it.
/// </summary>
[Collection(nameof(EndatixIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class JobsSchemaIntegrationTests(EndatixIntegrationWebHostFixture fixture) : IAsyncDisposable
{
    [Fact]
    public async Task BackgroundJobsTable_AfterMigrations_HasNoResultJsonColumn()
    {
        // Arrange — the collection fixture starts the host, and with it the migrations, before any test in it
        // runs, so the schema is already in place here.
        var cancellationToken = TestContext.Current.CancellationToken;
        var jsonType = fixture.Provider == TestDatabaseProvider.PostgreSql ? "jsonb" : "json";

        // Act
        var resultColumnExists = await IntegrationDbAssert.SqlRowExistsAsync(
            fixture.Database.ConnectionString,
            fixture.Database.Provider,
            BackgroundJobsColumnCount("COLUMN_NAME = 'ResultJson'"),
            cancellationToken);
        var payloadColumnIsJson = await IntegrationDbAssert.SqlRowExistsAsync(
            fixture.Database.ConnectionString,
            fixture.Database.Provider,
            BackgroundJobsColumnCount($"COLUMN_NAME = 'PayloadJson' AND DATA_TYPE = '{jsonType}'"),
            cancellationToken);

        // Assert — the row carries state only. Handler input stays, because the handler is given it.
        resultColumnExists.Should().BeFalse();
        payloadColumnIsJson.Should().BeTrue();
    }

    [Fact]
    public async Task Host_with_jobs_on_creates_the_scheduler_tables_and_runs_the_scheduler_while_its_background_work_is_on()
    {
        // Arrange — reset first, so the relay and the scheduler meet nothing an earlier test left.
        var cancellationToken = TestContext.Current.CancellationToken;
        await fixture.PrepareWorldAsync(IntegrationWorldOptions.Empty, cancellationToken);
        var schedulerTables = fixture.Provider == TestDatabaseProvider.PostgreSql
            ? QuartzSchemaScripts.PostgreSqlTableNames()
            : QuartzSchemaScripts.SqlServerTableNames();
        var scheduler = await fixture.Factory.Services
            .GetRequiredKeyedService<ISchedulerFactory>(QuartzRegistration.SchedulerName)
            .GetScheduler(cancellationToken);
        var beforeStart = scheduler.Status;

        // Act
        await fixture.BackgroundWork.StartJobsAsync(cancellationToken);
        var whileOn = scheduler.Status;
        await fixture.BackgroundWork.StopAsync(CancellationToken.None);
        var afterStop = scheduler.Status;
        var missingTables = await MissingTablesAsync(schedulerTables, cancellationToken);

        // Assert — the test host builds the scheduler without starting it; once a test has stopped it, it waits in standby.
        schedulerTables.Should().NotBeEmpty();
        missingTables.Should().BeEmpty();
        beforeStart.Should().BeOneOf(SchedulerStatus.Created, SchedulerStatus.Standby);
        whileOn.Should().Be(SchedulerStatus.Running);
        afterStop.Should().Be(SchedulerStatus.Standby);
    }

    /// <summary>Stops the shared host's background work even when the test failed before it did.</summary>
    public async ValueTask DisposeAsync() => await fixture.BackgroundWork.StopAsync(CancellationToken.None);

    private async Task<List<string>> MissingTablesAsync(IEnumerable<string> tables, CancellationToken cancellationToken)
    {
        List<string> missingTables = [];
        foreach (var table in tables)
        {
            if (!await IntegrationDbAssert.TableExistsAsync(
                    fixture.Database.ConnectionString, fixture.Database.Provider, JobsPersistence.Schema, table, cancellationToken))
            {
                missingTables.Add(table);
            }
        }

        return missingTables;
    }

    // Upper-case catalog names, which PostgreSQL folds to its own and SQL Server reads under any collation.
    private static string BackgroundJobsColumnCount(string condition) =>
        $"""
        SELECT count(*)
        FROM INFORMATION_SCHEMA.COLUMNS
        WHERE TABLE_SCHEMA = 'jobs'
          AND TABLE_NAME = 'BackgroundJobs'
          AND {condition}
        """;
}
