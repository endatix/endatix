using System.Reflection;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Endatix.Modules.Jobs.Persistence;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PostgreSqlUpgrade = Endatix.Modules.Jobs.Persistence.Migrations.PostgreSql.UpgradeQuartzTablesTo472;
using SqlServerUpgrade = Endatix.Modules.Jobs.Persistence.Migrations.SqlServer.UpgradeQuartzTablesTo472;

namespace Endatix.IntegrationTests;

/// <summary>
/// A database whose Quartz tables a 4.2.2 node created, brought to Quartz 4.7.2 by the <c>jobs</c> migrations.
/// </summary>
/// <remarks>
/// A fresh test database gets the 4.7.2 tables from <c>InitialBackgroundJobs</c>, which reads the create script of
/// the referenced Quartz, so the upgrade migration would only ever run as a no-op there. These tests migrate down
/// past it first, which leaves the 4.2.2 tables, and then up again.
/// </remarks>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class JobsQuartzSchemaUpgradeTests(DbIntegrationFixture fixture)
{
    [Fact]
    public async Task UpgradeMigration_DatabaseOnQuartz422Tables_LeavesTheTablesAFreshDatabaseGets()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var fresh = await AcquisitionTestStore.CreateAsync(fixture.Provider, fixture.ConnectionString, ct);
        await using var upgraded = await AcquisitionTestStore.CreateAsync(fixture.Provider, fixture.ConnectionString, ct);
        var (previousMigration, addedColumns) = UpgradeOf(fixture.Provider);
        await upgraded.MigrateToAsync(previousMigration, ct);
        var columnsOnQuartz422 = await ColumnsAsync(upgraded.Database, ct);

        // Act
        await upgraded.MigrateToAsync(targetMigration: null, ct);

        // Assert — the downgrade really left the 4.2.2 tables, and the upgrade brought back the rest.
        columnsOnQuartz422.Should().NotIntersectWith(addedColumns);
        await ShouldMatchAsync(upgraded.Database, fresh.Database, ct);
    }

    [Fact]
    public async Task UpgradeMigration_StatementFailsMidway_KeepsWhatCommittedAndCompletesWhenRunAgain()
    {
        // Arrange — the execution-history table is out of the way, so the upgrade's first statement on it fails, after
        // the earlier statements on the fired-trigger and trigger tables.
        var ct = TestContext.Current.CancellationToken;
        await using var fresh = await AcquisitionTestStore.CreateAsync(fixture.Provider, fixture.ConnectionString, ct);
        await using var upgraded = await AcquisitionTestStore.CreateAsync(fixture.Provider, fixture.ConnectionString, ct);
        var (previousMigration, addedColumns) = UpgradeOf(fixture.Provider);
        await upgraded.MigrateToAsync(previousMigration, ct);
        var history = HistoryTableOf(fixture.Provider);
        await upgraded.Database.ExecuteAsync(history.MoveAway, ct);
        var migrate = () => upgraded.MigrateToAsync(targetMigration: null, ct);

        // Act
        await migrate.Should().ThrowAsync<Exception>();
        var columnsAfterFailure = await ColumnsAsync(upgraded.Database, ct);
        var upgradeRecorded = await upgraded.Database.CountAsync(UpgradeRecordedCount(fixture.Provider), ct);
        await upgraded.Database.ExecuteAsync(history.BringBack, ct);
        await upgraded.MigrateToAsync(targetMigration: null, ct);

        // Assert — each statement before the failure committed on its own, the history row waited for the last one,
        // and the rerun completed the upgrade.
        columnsAfterFailure.Should().Contain(addedColumns[0]);
        upgradeRecorded.Should().Be(0);
        await ShouldMatchAsync(upgraded.Database, fresh.Database, ct);
    }

    [Fact]
    public async Task UpgradeMigration_TriggersStoredBeforeTheUpgrade_AreAcquiredAndTheSchedulerStarts()
    {
        // Arrange — triggers stored while the database is on the earlier tables, as a 4.2.2 node would leave them.
        var ct = TestContext.Current.CancellationToken;
        await using var store = await AcquisitionTestStore.CreateAsync(fixture.Provider, fixture.ConnectionString, ct);
        var behind = await store.SeedHeadOfLineAsync(ct);
        await store.MigrateToAsync(UpgradeOf(fixture.Provider).PreviousMigration, ct);

        // Act
        await store.MigrateToAsync(targetMigration: null, ct);
        var acquired = await store.SelectTriggersToAcquireAsync(2, limits => limits.ForGroup("A", 0).ForGroup("B", 1), ct);
        await using var node = JobsTestNode.Create(store.Database);
        var start = () => node.StartAsync(ct);

        // Assert — Quartz validates its tables at startup and refuses any it finds without a 4.7.2 column.
        acquired.Should().Equal(behind);
        await start.Should().NotThrowAsync();
    }

    // Statements that rename Quartz's execution-history table and rename it back.
    private static (string MoveAway, string BringBack) HistoryTableOf(TestDatabaseProvider provider) => provider switch
    {
        TestDatabaseProvider.PostgreSql => (
            "ALTER TABLE jobs.qrtz_execution_history RENAME TO qrtz_execution_history_away;",
            "ALTER TABLE jobs.qrtz_execution_history_away RENAME TO qrtz_execution_history;"),
        TestDatabaseProvider.SqlServer => (
            "EXEC sp_rename N'jobs.qrtz_EXECUTION_HISTORY', N'qrtz_EXECUTION_HISTORY_AWAY';",
            "EXEC sp_rename N'jobs.qrtz_EXECUTION_HISTORY_AWAY', N'qrtz_EXECUTION_HISTORY';"),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported test database provider."),
    };

    private static string UpgradeRecordedCount(TestDatabaseProvider provider)
    {
        var id = (provider == TestDatabaseProvider.PostgreSql ? typeof(PostgreSqlUpgrade) : typeof(SqlServerUpgrade))
            .GetCustomAttribute<MigrationAttribute>()!.Id;
        return provider == TestDatabaseProvider.PostgreSql
            ? $"""SELECT count(*) FROM jobs."__EFMigrationsHistory" WHERE "MigrationId" = '{id}'"""
            : $"SELECT count(*) FROM jobs.__EFMigrationsHistory WHERE MigrationId = '{id}'";
    }

    // Every column, table and index the 4.7.2 create script makes, with the same types.
    private static async Task ShouldMatchAsync(JobsTestDatabase actual, JobsTestDatabase expected, CancellationToken ct)
    {
        (await ColumnsAsync(actual, ct)).Should().BeEquivalentTo(await ColumnsAsync(expected, ct));
        (await ColumnDefinitionsAsync(actual, ct)).Should().BeEquivalentTo(await ColumnDefinitionsAsync(expected, ct));
        (await IndexesAsync(actual, ct)).Should().BeEquivalentTo(await IndexesAsync(expected, ct));
    }

    // The migration before the upgrade, which a database on Quartz 4.2.2's tables was last migrated to, and the
    // columns the upgrade adds, as "table.column" in the case the catalog spells them.
    private static (string PreviousMigration, string[] AddedColumns) UpgradeOf(TestDatabaseProvider provider) => provider switch
    {
        TestDatabaseProvider.PostgreSql => (
            MigrationBefore(typeof(PostgreSqlUpgrade), typeof(JobsPostgreSqlDbContext)),
            PostgreSqlUpgrade.Columns.Select(column => $"{column.Table}.{column.Column}").ToArray()),
        TestDatabaseProvider.SqlServer => (
            MigrationBefore(typeof(SqlServerUpgrade), typeof(JobsSqlServerDbContext)),
            SqlServerUpgrade.Columns.Select(column => $"{column.Table}.{column.Column}").ToArray()),
        _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported test database provider."),
    };

    // The id of the migration right before the upgrade in its context's chain.
    private static string MigrationBefore(Type upgrade, Type context)
    {
        var ids = upgrade.Assembly.GetTypes()
            .Where(type => type.GetCustomAttribute<DbContextAttribute>()?.ContextType == context)
            .Select(type => type.GetCustomAttribute<MigrationAttribute>()?.Id)
            .OfType<string>()
            .Order(StringComparer.Ordinal)
            .ToList();
        return ids[ids.IndexOf(upgrade.GetCustomAttribute<MigrationAttribute>()!.Id) - 1];
    }

    private static Task<List<string>> ColumnsAsync(JobsTestDatabase database, CancellationToken ct) =>
        database.QueryAsync(
            database.Provider == TestDatabaseProvider.PostgreSql
                ? """
                  SELECT table_name || '.' || column_name FROM information_schema.columns
                  WHERE table_schema = 'jobs' AND table_name LIKE 'qrtz\_%'
                  """
                : """
                  SELECT t.name + '.' + c.name FROM sys.columns c JOIN sys.tables t ON t.object_id = c.object_id
                  WHERE SCHEMA_NAME(t.schema_id) = 'jobs' AND t.name LIKE 'qrtz[_]%'
                  """,
            reader => reader.GetString(0),
            ct);

    private static Task<List<string>> ColumnDefinitionsAsync(JobsTestDatabase database, CancellationToken ct) =>
        database.QueryAsync(
            database.Provider == TestDatabaseProvider.PostgreSql
                ? """
                  SELECT table_name || '.' || column_name || ' ' || data_type || ' '
                      || COALESCE(character_maximum_length::text, '-') || ' ' || is_nullable || ' '
                      || COALESCE(column_default, '-')
                  FROM information_schema.columns
                  WHERE table_schema = 'jobs' AND table_name LIKE 'qrtz\_%'
                  """
                : """
                  SELECT t.name + '.' + c.name + ' ' + ty.name + ' ' + CAST(c.max_length AS varchar(10)) + ' '
                      + CAST(c.is_nullable AS varchar(1)) + ' ' + COALESCE(c.collation_name, '-') + ' '
                      + COALESCE(d.definition, '-')
                  FROM sys.columns c
                  JOIN sys.tables t ON t.object_id = c.object_id
                  JOIN sys.types ty ON ty.user_type_id = c.user_type_id
                  LEFT JOIN sys.default_constraints d ON d.object_id = c.default_object_id
                  WHERE SCHEMA_NAME(t.schema_id) = 'jobs' AND t.name LIKE 'qrtz[_]%'
                  """,
            reader => reader.GetString(0),
            ct);

    // Each index by name with its definition (PostgreSQL), or its key columns in order and its included columns
    // (SQL Server).
    private static Task<List<string>> IndexesAsync(JobsTestDatabase database, CancellationToken ct) =>
        database.QueryAsync(
            database.Provider == TestDatabaseProvider.PostgreSql
                ? """
                  SELECT indexname || ' ' || indexdef FROM pg_indexes
                  WHERE schemaname = 'jobs' AND tablename LIKE 'qrtz\_%'
                  """
                : """
                  SELECT t.name + '.' + i.name + ' ' + STRING_AGG(
                      IIF(ic.is_included_column = 1, '+', '') + c.name, ', ')
                      WITHIN GROUP (ORDER BY ic.is_included_column, ic.key_ordinal, c.name)
                  FROM sys.indexes i
                  JOIN sys.tables t ON t.object_id = i.object_id
                  JOIN sys.index_columns ic ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                  JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                  WHERE SCHEMA_NAME(t.schema_id) = 'jobs' AND t.name LIKE 'qrtz[_]%'
                  GROUP BY t.name, i.name
                  """,
            reader => reader.GetString(0),
            ct);
}
