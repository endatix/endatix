using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Reporting.Persistence;
using Endatix.Modules.Reporting.Persistence.Migrations.PostgreSql;
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
    public void GetMigrations_PostgreSqlContext_ReturnsTheFourShippedIds()
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
            "20260930070117_AddFlattenedSubmissionSourceRevision");
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
    public void GetMigrations_SqlServerContext_SeesNoPostgreSqlMigrations()
    {
        // Arrange
        using var postgreSqlContext = CreatePostgreSqlContext();
        using var sqlServerContext = new ReportingSqlServerDbContext(
            HostOptions<ReportingSqlServerDbContext>(
                "sqlserver",
                "Server=127.0.0.1;Database=not_connected;User Id=sa;Password=not-connected"),
            Substitute.For<ITenantContext>());

        // Act
        var sqlServerMigrationIds = sqlServerContext.Database.GetMigrations();
        var sqlServerSnapshot = sqlServerContext.GetService<IMigrationsAssembly>().ModelSnapshot;

        // Assert
        sqlServerMigrationIds.Should().NotIntersectWith(
            postgreSqlContext.Database.GetMigrations(),
            "each provider's context must see only its own migrations");
        (sqlServerSnapshot?.GetType()).Should().NotBe<ReportingPostgreSqlDbContextModelSnapshot>();
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
