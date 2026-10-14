using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Persistence.Migrations.SqlServer;
using Microsoft.Data.SqlClient;

namespace Endatix.IntegrationTests;

/// <summary>Acquisition on a database whose default collation changed after the jobs migrations ran.</summary>
public sealed partial class JobsSqlServerStoreTests
{
    // Differs from the default the test servers create databases with, which the scheduler's tables keep.
    private const string ChangedCollation = "Latin1_General_100_CS_AS";

    [Fact]
    public async Task SelectTriggersToAcquire_DatabaseCollationChangedAfterMigrating_ReturnsOtherGroupsTriggerWithFlatReads()
    {
        // Arrange — group A is full and its backlog is due before B's only trigger, as in the flat-reads test.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.SqlServer, SkipReason);
        var ct = TestContext.Current.CancellationToken;
        await using var store = await AcquisitionTestStore.CreateAsync(TestDatabaseProvider.SqlServer, fixture.ConnectionString, ct);
        await store.ScheduleAsync(AcquisitionTestTrigger.Of("A", BacklogTemplate, 0), ct);
        var behind = AcquisitionTestTrigger.Of("B", "b1", 1500);
        await store.ScheduleAsync(behind, ct);
        await ChangeDatabaseCollationAsync(store.Database, ct);

        // Act
        var acquired = await store.SelectTriggersToAcquireAsync(11, limits => limits.ForGroup("A", 0).ForGroup("B", 1), ct);
        var acquisitions = await AcquireAsBacklogGrowsAsync(store.Database, ct);

        // Assert — the tables kept the collation they were created in, which now differs from the database's.
        var (databaseCollation, tableCollation) = await CollationsAsync(store.Database, ct);
        databaseCollation.Should().Be(ChangedCollation);
        tableCollation.Should().NotBe(ChangedCollation);
        acquired.Should().Equal(behind.Key);
        ShouldReturnOnlyAndReadFlat(acquisitions, behind.Key);
    }

    // SQL Server refuses to change a database's collation while any schema-bound object depends on it: here the job
    // table's check constraint and filtered dedup index, and the acquisition column with its index. The job table's
    // two are dropped for good, since acquisition never reads them and the database is the test's own; the
    // acquisition column and index are created again as the migration creates them. The change needs the database to
    // itself, which ends every other connection to it, so the pools forget theirs.
    private static async Task ChangeDatabaseCollationAsync(JobsTestDatabase database, CancellationToken ct)
    {
        await database.ExecuteAsync(
            """
            ALTER TABLE jobs.BackgroundJobs DROP CONSTRAINT CK_BackgroundJobs_TenantId;
            DROP INDEX IX_BackgroundJobs_DedupKey ON jobs.BackgroundJobs;
            """,
            ct);
        await database.ExecuteAsync(InitialBackgroundJobs.DropAcquireIndexAndColumn, ct);
        await database.ExecuteAsync(
            $"""
            ALTER DATABASE CURRENT SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
            ALTER DATABASE CURRENT COLLATE {ChangedCollation};
            ALTER DATABASE CURRENT SET MULTI_USER;
            """,
            ct);
        SqlConnection.ClearAllPools();
        await database.ExecuteAsync(InitialBackgroundJobs.AddAcquireGroupColumn, ct);
        await database.ExecuteAsync(InitialBackgroundJobs.CreateAcquireIndex, ct);
    }

    // The database's default collation, and that of a column the acquisition column is computed from.
    private static async Task<(string Database, string Table)> CollationsAsync(JobsTestDatabase database, CancellationToken ct)
    {
        var rows = await database.QueryAsync(
            """
            SELECT CONVERT(nvarchar(128), DATABASEPROPERTYEX(DB_NAME(), 'Collation')),
                (SELECT collation_name FROM sys.columns WHERE object_id = OBJECT_ID('jobs.qrtz_TRIGGERS') AND name = 'JOB_NAME')
            """,
            reader => (reader.GetString(0), reader.GetString(1)),
            ct);
        return rows.Single();
    }
}
