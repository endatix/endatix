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
public sealed class JobsSchemaIntegrationTests(EndatixIntegrationWebHostFixture fixture)
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
    public async Task Host_with_jobs_on_creates_the_scheduler_tables_and_starts_the_scheduler()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var schedulerTables = fixture.Provider == TestDatabaseProvider.PostgreSql
            ? QuartzSchemaScripts.PostgreSqlTableNames()
            : QuartzSchemaScripts.SqlServerTableNames();

        // Act
        var scheduler = await fixture.Factory.Services
            .GetRequiredKeyedService<ISchedulerFactory>(QuartzRegistration.SchedulerName)
            .GetScheduler(cancellationToken);
        List<string> missingTables = [];
        foreach (var table in schedulerTables)
        {
            if (!await IntegrationDbAssert.TableExistsAsync(
                    fixture.Database.ConnectionString, fixture.Database.Provider, JobsPersistence.Schema, table, cancellationToken))
            {
                missingTables.Add(table);
            }
        }

        // Assert
        schedulerTables.Should().NotBeEmpty();
        missingTables.Should().BeEmpty();
        scheduler.Status.Should().Be(SchedulerStatus.Running);
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
