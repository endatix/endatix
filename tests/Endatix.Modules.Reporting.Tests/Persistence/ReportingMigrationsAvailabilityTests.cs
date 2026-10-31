using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Reporting.Persistence;
using Endatix.Modules.Reporting.Persistence.Migrations.PostgreSql;
using Endatix.Modules.Reporting.Persistence.Migrations.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;

namespace Endatix.Modules.Reporting.Tests.Persistence;

/// <summary>
/// The contexts are configured the way the host configures them, so they discover migrations and the
/// snapshot exactly as startup does. No connection is opened.
/// </summary>
public sealed class ReportingMigrationsAvailabilityTests
{
    private const string MigrationsRootNamespace = "Endatix.Modules.Reporting.Persistence.Migrations";

    [Fact]
    public void GetMigrations_PostgreSqlContext_ReturnsItsChainInOrder()
    {
        // Arrange
        using var context = CreatePostgreSqlContext();

        // Act
        var migrationIds = context.Database.GetMigrations();

        // Assert
        migrationIds.Should().Equal(
            "20260721062723_InitialReporting",
            "20260904194346_SeedDefaultExportFormats",
            "20260929125737_AddFlattenedSubmissionSourceModifiedAt",
            "20260930070117_AddFlattenedSubmissionSourceRevision",
            "20261016050012_AddSubmissionViews",
            "20261016102336_AddSearchCopy",
            "20261016164018_AddSearchKeysRevision");
    }

    [Fact]
    public void HasPendingModelChanges_PostgreSqlContext_ReturnsFalse()
    {
        // Arrange
        using var context = CreatePostgreSqlContext();

        // Act
        var snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot;
        var hasPendingModelChanges = context.Database.HasPendingModelChanges();

        // Assert
        snapshot.Should().BeOfType<ReportingPostgreSqlDbContextModelSnapshot>();
        hasPendingModelChanges.Should().BeFalse();
    }

    [Fact]
    public void GetMigrations_SqlServerContext_ReturnsOnlyItsOwnChain()
    {
        // Arrange
        using var postgreSqlContext = CreatePostgreSqlContext();
        using var sqlServerContext = CreateSqlServerContext();

        // Act
        var sqlServerMigrationIds = sqlServerContext.Database.GetMigrations().ToList();

        // Assert
        sqlServerMigrationIds.Should().Equal(
            "20261012154208_InitialReporting",
            "20261016050017_AddSubmissionViews",
            "20261016102338_AddSearchCopy",
            "20261016164020_AddSearchKeysRevision");
        sqlServerMigrationIds.Should().NotIntersectWith(
            postgreSqlContext.Database.GetMigrations(),
            "each provider's context must see only its own migrations");
    }

    [Fact]
    public void HasPendingModelChanges_SqlServerContext_ReturnsFalse()
    {
        // Arrange
        using var context = CreateSqlServerContext();

        // Act
        var snapshot = context.GetService<IMigrationsAssembly>().ModelSnapshot;
        var hasPendingModelChanges = context.Database.HasPendingModelChanges();

        // Assert
        snapshot.Should().BeOfType<ReportingSqlServerDbContextModelSnapshot>();
        hasPendingModelChanges.Should().BeFalse();
    }

    [Fact]
    public void ConfigureDbContextOptions_BothProviders_UseTheMigrationsRootNamespace()
    {
        // Arrange
        var options = new ModuleDbContextOptions();

        // Act
        ReportingPersistence.ConfigureDbContextOptions(options);

        // Assert
        options.PostgreSqlMigrationsNamespace.Should().Be(MigrationsRootNamespace);
        options.SqlServerMigrationsNamespace.Should().Be(MigrationsRootNamespace);
    }

    private static ReportingPostgreSqlDbContext CreatePostgreSqlContext() =>
        new(
            HostOptions<ReportingPostgreSqlDbContext>(
                "postgresql",
                "Host=127.0.0.1;Database=not_connected;Username=postgres;Password=not-connected"),
            Substitute.For<ITenantContext>());

    private static ReportingSqlServerDbContext CreateSqlServerContext() =>
        new(
            HostOptions<ReportingSqlServerDbContext>(
                "sqlserver",
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
        optionsBuilder.ConfigureModuleDbContext(configuration, ReportingPersistence.ConfigureDbContextOptions);
        return optionsBuilder.Options;
    }
}
