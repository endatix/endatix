using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.Extensions.Configuration;

namespace Endatix.Modules.Audience.Tests.Persistence;

/// <summary>
/// The previous release keeps writing while a release migrates (ARCHITECTURE.md, Schema changes during a rolling
/// deploy), so a migration never adds a column and a constraint on it at once. No connection is opened.
/// </summary>
public sealed class AudienceMigrationsRollingDeployTests
{
    // Shipped with the module, before its first production release, so no previous release writes these tables.
    private static readonly string[] _shippedWithTheModule = ["AddPropertyValueIsTest"];

    public static TheoryData<string> Providers => new() { "postgresql", "sqlserver" };

    [Theory]
    [MemberData(nameof(Providers))]
    public void Up_EveryMigration_ConstrainsNoColumnItAdds(string provider)
    {
        // Arrange
        using var context = CreateContext(provider);

        // Act
        var violations = UpOperationsByMigration(context)
            .Where(migration => !_shippedWithTheModule.Contains(migration.Name))
            .SelectMany(migration => ConstraintsOnAddedColumns(migration.Operations)
                .Select(constraint => $"{migration.Name}: {constraint}"));

        // Assert
        violations.Should().BeEmpty("a constraint on a new column ships one release after the column");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void Up__shippedWithTheModule_ConstrainsTheColumnsItAdds(string provider)
    {
        // Arrange
        using var context = CreateContext(provider);

        // Act
        var exempted = UpOperationsByMigration(context)
            .Where(migration => _shippedWithTheModule.Contains(migration.Name))
            .Select(migration => (migration.Name, Constraints: ConstraintsOnAddedColumns(migration.Operations)))
            .ToList();

        // Assert
        exempted.Select(migration => migration.Name).Should().BeEquivalentTo(_shippedWithTheModule);
        exempted.Should().OnlyContain(migration => migration.Constraints.Count > 0, "an exemption that matches nothing is stale");
    }

    [Theory]
    [MemberData(nameof(Providers))]
    public void Up_EveryMigration_AddsNoRequiredColumnWithoutADefault(string provider)
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
                .Select(column => $"{migration.Name}: {column.Table}.{column.Name}"));

        // Assert
        violations.Should().BeEmpty("the previous release inserts rows without the new column");
    }

    private static List<string> ConstraintsOnAddedColumns(IReadOnlyList<MigrationOperation> operations)
    {
        var added = operations
            .OfType<AddColumnOperation>()
            .Select(column => (column.Table, column.Name))
            .ToHashSet();
        bool CoversAnAddedColumn(string table, IEnumerable<string> columns) =>
            columns.Any(column => added.Contains((table, column)));

        var foreignKeys = operations.OfType<AddForeignKeyOperation>()
            .Where(key => CoversAnAddedColumn(key.Table, key.Columns))
            .Select(key => key.Name);
        var uniqueConstraints = operations.OfType<AddUniqueConstraintOperation>()
            .Where(key => CoversAnAddedColumn(key.Table, key.Columns))
            .Select(key => key.Name);
        var uniqueIndexes = operations.OfType<CreateIndexOperation>()
            .Where(index => index.IsUnique && CoversAnAddedColumn(index.Table, index.Columns))
            .Select(index => index.Name);
        var checks = operations.OfType<AddCheckConstraintOperation>()
            .Where(check => added.Any(column => column.Table == check.Table && check.Sql.Contains(column.Name)))
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
            ? new AudiencePostgreSqlDbContext(
                HostOptions<AudiencePostgreSqlDbContext>(
                    provider,
                    "Host=127.0.0.1;Database=not_connected;Username=postgres;Password=not-connected"),
                Substitute.For<ITenantContext>())
            : new AudienceSqlServerDbContext(
                HostOptions<AudienceSqlServerDbContext>(
                    provider,
                    "Server=127.0.0.1;Database=not_connected;User Id=sa;Password=not-connected"),
                Substitute.For<ITenantContext>());

    private static DbContextOptions<TContext> HostOptions<TContext>(string provider, string connectionString)
        where TContext : DbContext
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = connectionString,
                ["ConnectionStrings:DefaultConnection_DbProvider"] = provider,
            })
            .Build();
        var optionsBuilder = new DbContextOptionsBuilder<TContext>();
        optionsBuilder.ConfigureModuleDbContext(configuration, AudiencePersistence.ConfigureDbContextOptions);
        return optionsBuilder.Options;
    }
}
