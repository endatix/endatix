using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Endatix.Modules.Audience.Tests.Persistence;

/// <summary>
/// The previous release keeps writing while a release migrates (ARCHITECTURE.md, Schema changes during a rolling
/// deploy), so a migration never adds a column and a constraint on it at once. It sees one migration at a time;
/// two migrations of one release are the PR review's to catch.
/// </summary>
public sealed class AudienceMigrationsRollingDeployTests
{
    // Shipped while the module was a preview behind personalization-module (off by default), so no production
    // host wrote PropertyValues before it (endatix#1389). Its AK_Memberships_TestFlag also constrains
    // Memberships.IsTest, which AddMembershipIsTest added in the same release; this test cannot see that pair.
    private static readonly string[] _exempted = ["AddPropertyValueIsTest"];

    public static TheoryData<string> Providers => new() { "postgresql", "sqlserver" };

    [Theory]
    [MemberData(nameof(Providers))]
    public void Up_EveryMigration_ConstrainsNoColumnItAdds(string provider)
    {
        // Arrange
        using var context = CreateContext(provider);

        // Act
        var violations = UpOperationsByMigration(context)
            .Where(migration => !_exempted.Contains(migration.Name))
            .SelectMany(migration => ConstraintsOnAddedColumns(migration.Operations)
                .Select(constraint => $"{migration.Name}: {constraint}"))
            .ToList();

        // Assert
        violations.Should().BeEmpty("a constraint on a new column ships one stable release after the column");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void Up_EveryMigration_GivesARequiredAddedColumnADatabaseDefault(string provider)
    {
        // Arrange
        using var context = CreateContext(provider);

        // Act
        var violations = UpOperationsByMigration(context)
            .SelectMany(migration => migration.Operations
                .OfType<AddColumnOperation>()
                .Where(column => !column.IsNullable
                    && column.DefaultValue is null
                    && column.DefaultValueSql is null
                    && column.ComputedColumnSql is null)
                .Select(column => $"{migration.Name}: {column.Table}.{column.Name}"))
            .ToList();

        // Assert
        violations.Should().BeEmpty("the previous release's inserts omit the column, so it needs a default or NULL");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void Up_ExemptedMigrations_StillConstrainAColumnTheyAdd(string provider)
    {
        // Arrange
        using var context = CreateContext(provider);

        // Act
        var exempted = UpOperationsByMigration(context)
            .Where(migration => _exempted.Contains(migration.Name))
            .Select(migration => (migration.Name, Constraints: ConstraintsOnAddedColumns(migration.Operations)))
            .ToList();

        // Assert
        exempted.Select(migration => migration.Name).Should().BeEquivalentTo(_exempted);
        exempted.Should().OnlyContain(migration => migration.Constraints.Count > 0, "an exemption that matches nothing is stale");
    }

    private static List<string> ConstraintsOnAddedColumns(IReadOnlyList<MigrationOperation> operations)
    {
        var added = operations
            .OfType<AddColumnOperation>()
            .Select(column => (column.Schema, column.Table, column.Name))
            .ToHashSet();
        bool CoversAnAddedColumn(string? schema, string table, IEnumerable<string> columns) =>
            columns.Any(column => added.Contains((schema, table, column)));
        // Raw SQL may quote the name ("IsTest", [IsTest]) or not (IsTest = 0), so match it as a whole word.
        bool NamesAnAddedColumn(string? schema, string table, string? sql) =>
            sql is not null && added.Any(column => column.Schema == schema && column.Table == table
                && Regex.IsMatch(sql, $@"\b{Regex.Escape(column.Name)}\b", RegexOptions.IgnoreCase));

        var foreignKeys = operations.OfType<AddForeignKeyOperation>()
            .Where(key => CoversAnAddedColumn(key.Schema, key.Table, key.Columns))
            .Select(key => key.Name);
        var uniqueConstraints = operations.OfType<AddUniqueConstraintOperation>()
            .Where(key => CoversAnAddedColumn(key.Schema, key.Table, key.Columns))
            .Select(key => key.Name);
        var uniqueIndexes = operations.OfType<CreateIndexOperation>()
            .Where(index => index.IsUnique
                && (CoversAnAddedColumn(index.Schema, index.Table, index.Columns)
                    || NamesAnAddedColumn(index.Schema, index.Table, index.Filter)))
            .Select(index => index.Name);
        var checks = operations.OfType<AddCheckConstraintOperation>()
            .Where(check => NamesAnAddedColumn(check.Schema, check.Table, check.Sql))
            .Select(check => check.Name);

        return [.. foreignKeys, .. uniqueConstraints, .. uniqueIndexes, .. checks];
    }

    private static IEnumerable<(string Name, IReadOnlyList<MigrationOperation> Operations)> UpOperationsByMigration(
        DbContext context)
    {
        var migrationsAssembly = context.GetService<IMigrationsAssembly>();
        var activeProvider = context.Database.ProviderName!;
        return migrationsAssembly.Migrations.Values.Select(type =>
        {
            var migration = migrationsAssembly.CreateMigration(type, activeProvider);
            return (type.Name, migration.UpOperations);
        });
    }

    private static DbContext CreateContext(string provider) =>
        provider == "postgresql"
            ? NotConnectedAudienceContexts.CreatePostgreSql()
            : NotConnectedAudienceContexts.CreateSqlServer();
}
