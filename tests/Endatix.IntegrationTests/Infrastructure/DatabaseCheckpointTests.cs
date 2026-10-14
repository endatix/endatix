using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Persistence;

namespace Endatix.IntegrationTests;

/// <summary>
/// What a reset between tests clears in a jobs schema, and what it leaves to the scheduler that owns it.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
[Trait("DbSpecific", "SqlServer")]
public sealed class DatabaseCheckpointTests(DbIntegrationFixture fixture)
{
    private const string SkipReason = "Resets a jobs schema created by the SQL Server migrations.";

    private const string MigrationsHistory = "__EFMigrationsHistory";

    private const string InsertThreeJobs =
        """
        INSERT INTO jobs.BackgroundJobs
            (Id, JobType, TenantId, Status, PayloadJson, ProgressPercentage, AttemptCount, NextAttemptAt, CreatedAt, IsDeleted)
        VALUES
            (1, N'A', 1, 0, N'{}', 0, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 0),
            (2, N'A', 1, 0, N'{}', 0, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 0),
            (3, N'B', 1, 0, N'{}', 0, 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 0)
        """;

    [Fact]
    public async Task ResetAsync_SqlServerJobsSchema_ClearsJobRowsAndKeepsSchedulerTables()
    {
        // Arrange
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.SqlServer, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var store = await AcquisitionTestStore.CreateAsync(TestDatabaseProvider.SqlServer, fixture.ConnectionString, ct);
        await store.ScheduleAsync(AcquisitionTestTrigger.Of("A", "a1", 0), ct);
        await store.Database.ExecuteAsync(InsertThreeJobs, ct);
        var keptBefore = await RowsPerKeptTableAsync(store.Database, ct);

        // Act
        await new DatabaseCheckpoint().ResetAsync(store.Database.ConnectionString, TestDatabaseProvider.SqlServer, ct);

        // Assert
        (await store.Database.CountAsync("SELECT count(*) FROM jobs.BackgroundJobs", ct)).Should().Be(0);
        keptBefore.Keys.Should().Contain(QuartzSchemaScripts.SqlServerTableNames()).And.Contain(MigrationsHistory);
        keptBefore["qrtz_JOB_DETAILS"].Should().BePositive();
        keptBefore["qrtz_TRIGGERS"].Should().BePositive();
        keptBefore[MigrationsHistory].Should().BePositive();
        (await RowsPerKeptTableAsync(store.Database, ct)).Should().Equal(keptBefore);
    }

    // Every table of the jobs schema except the job rows themselves: the scheduler's and the migrations history.
    private static async Task<Dictionary<string, long>> RowsPerKeptTableAsync(JobsTestDatabase database, CancellationToken ct)
    {
        var tables = await database.QueryAsync(
            "SELECT name FROM sys.tables WHERE schema_id = SCHEMA_ID('jobs') AND name <> 'BackgroundJobs'",
            reader => reader.GetString(0),
            ct);
        var rows = new Dictionary<string, long>();
        foreach (var table in tables)
        {
            rows[table] = await database.CountAsync($"SELECT count(*) FROM jobs.[{table}]", ct);
        }

        return rows;
    }
}
