using Endatix.Core.Abstractions;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Audience.Persistence;
using Endatix.Modules.Audience.Persistence.Migrations.PostgreSql;
using Endatix.Modules.Audience.Persistence.Migrations.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;

namespace Endatix.Modules.Audience.Tests.Persistence;

/// <summary>
/// The contexts are configured the way the host configures them, so they discover migrations and the
/// snapshot exactly as startup does. No connection is opened.
/// </summary>
public sealed class AudienceMigrationsAvailabilityTests
{
    private const string MigrationsRootNamespace = "Endatix.Modules.Audience.Persistence.Migrations";

    [Fact]
    public void GetMigrations_PostgreSqlContext_ReturnsTheShippedIds()
    {
        // Arrange
        using var context = CreatePostgreSqlContext();

        // Act
        var migrationIds = context.Database.GetMigrations();

        // Assert
        migrationIds.Should().Equal(
            "20260930105720_InitialPersonalization",
            "20260930112557_AddAudienceImports",
            "20261008074014_AddAudienceLinks");
    }

    [Fact]
    public void GetMigrations_SqlServerContext_ReturnsOnlyItsInitialMigration()
    {
        // Arrange
        using var context = CreateSqlServerContext();
        using var postgreSqlContext = CreatePostgreSqlContext();

        // Act
        var migrationIds = context.Database.GetMigrations().ToList();

        // Assert
        migrationIds.Should().ContainSingle().Which.Should().EndWith("_InitialPersonalization");
        migrationIds.Should().NotIntersectWith(postgreSqlContext.Database.GetMigrations());
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
        snapshot.Should().BeOfType<AudiencePostgreSqlDbContextModelSnapshot>();
        hasPendingModelChanges.Should().BeFalse();
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
        snapshot.Should().BeOfType<AudienceSqlServerDbContextModelSnapshot>();
        hasPendingModelChanges.Should().BeFalse();
    }

    [Fact]
    public void ConfigureDbContextOptions_BothProviders_UseTheMigrationsRootNamespace()
    {
        // Arrange
        var options = new ModuleDbContextOptions();

        // Act
        AudiencePersistence.ConfigureDbContextOptions(options);

        // Assert
        options.PostgreSqlMigrationsNamespace.Should().Be(MigrationsRootNamespace);
        options.SqlServerMigrationsNamespace.Should().Be(MigrationsRootNamespace);
    }

    private static AudiencePostgreSqlDbContext CreatePostgreSqlContext() =>
        new(
            HostOptions<AudiencePostgreSqlDbContext>(
                "postgresql",
                "Host=127.0.0.1;Database=not_connected;Username=postgres;Password=not-connected"),
            Substitute.For<ITenantContext>());

    private static AudienceSqlServerDbContext CreateSqlServerContext() =>
        new(
            HostOptions<AudienceSqlServerDbContext>(
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
        optionsBuilder.ConfigureModuleDbContext(configuration, AudiencePersistence.ConfigureDbContextOptions);
        return optionsBuilder.Options;
    }
}
