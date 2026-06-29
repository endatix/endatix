using System.Reflection;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Endatix.Modules.Reporting.Tests.Persistence;

public sealed class ReportingMigrationsAvailabilityTests
{
    [Fact]
    public void PostgreSqlMigrationsNamespace_HasMigrations()
    {
        // Arrange
        var assembly = typeof(ReportingDbContext).Assembly;

        // Act
        var migrationTypes = assembly.GetTypes()
            .Where(type => typeof(Migration).IsAssignableFrom(type) && !type.IsAbstract)
            .Where(type => type.Namespace == ReportingPersistence.PostgreSqlMigrationsNamespace)
            .ToList();

        // Assert
        migrationTypes.Should().NotBeEmpty("PostgreSQL Reporting migrations should be present");
    }

    [Fact]
    public void SqlServerMigrationsNamespace_HasNoMigrations_UntilIssue813()
    {
        // Arrange
        var assembly = typeof(ReportingDbContext).Assembly;

        // Act
        var migrationTypes = assembly.GetTypes()
            .Where(type => typeof(Migration).IsAssignableFrom(type) && !type.IsAbstract)
            .Where(type => type.Namespace == ReportingPersistence.SqlServerMigrationsNamespace)
            .ToList();

        // Assert
        migrationTypes.Should().BeEmpty(
            "SQL Server Reporting migrations are tracked in https://github.com/endatix/endatix/issues/813");
    }
}
