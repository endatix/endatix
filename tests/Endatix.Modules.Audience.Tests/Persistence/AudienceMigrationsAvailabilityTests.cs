using Endatix.Infrastructure.Data;
using Endatix.Modules.Audience.Persistence;
using Endatix.Modules.Audience.Persistence.Migrations.PostgreSql;
using Endatix.Modules.Audience.Persistence.Migrations.SqlServer;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Endatix.Modules.Audience.Tests.Persistence;

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
            "20261008074014_AddAudienceLinks",
            "20261019002103_AddMembershipIsTest",
            "20261024135627_AddPropertyValueIsTest",
            "20261025201021_AddStagedAudienceImports",
            "20261025215922_FenceImportWritesOnWriteRun",
            "20261026191842_AddImportRunsInRequest",
            "20261026221758_AddImportOptions");
    }

    [Fact]
    public void GetMigrations_SqlServerContext_ReturnsItsOwnShippedIds()
    {
        // Arrange
        using var context = CreateSqlServerContext();
        using var postgreSqlContext = CreatePostgreSqlContext();

        // Act
        var migrationIds = context.Database.GetMigrations().ToList();

        // Assert
        migrationIds.Should().Equal(
            "20261012154511_InitialPersonalization",
            "20261019002106_AddMembershipIsTest",
            "20261024135630_AddPropertyValueIsTest",
            "20261025201026_AddStagedAudienceImports",
            "20261025215942_FenceImportWritesOnWriteRun",
            "20261026191848_AddImportRunsInRequest",
            "20261026221808_AddImportOptions");
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

    private static AudiencePostgreSqlDbContext CreatePostgreSqlContext() => NotConnectedAudienceContexts.CreatePostgreSql();

    private static AudienceSqlServerDbContext CreateSqlServerContext() => NotConnectedAudienceContexts.CreateSqlServer();
}
