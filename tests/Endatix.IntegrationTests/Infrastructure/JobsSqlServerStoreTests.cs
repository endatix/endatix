using Endatix.Infrastructure.Data;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;

namespace Endatix.IntegrationTests;

/// <summary>
/// The Background Jobs store on SQL Server, at database level: what the SQL Server migrations create and remove.
/// Each test migrates a database of its own, so only that migration shapes it.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "SqlServer")]
public sealed class JobsSqlServerStoreTests(DbIntegrationFixture fixture)
{
    private const string SkipReason = "Asserts SQL Server catalog views and T-SQL.";

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
