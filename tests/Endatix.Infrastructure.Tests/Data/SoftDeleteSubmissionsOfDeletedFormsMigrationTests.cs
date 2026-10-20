using FluentAssertions;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using PostgreSqlMigration = Endatix.Persistence.PostgreSql.Migrations.AppEntities.SoftDeleteSubmissionsOfDeletedForms;
using SqlServerMigration = Endatix.Persistence.SqlServer.Migrations.AppEntities.SoftDeleteSubmissionsOfDeletedForms;

namespace Endatix.Infrastructure.Tests.Data;

/// <summary>How each provider's data migration holds its locks; its effect is tested against a real database.</summary>
public sealed class SoftDeleteSubmissionsOfDeletedFormsMigrationTests
{
    [Fact]
    public void Up_OnSqlServer_CommitsEachBatchOutsideTheMigrationsTransaction()
    {
        // Arrange
        var migration = new SqlServerMigration();

        // Act
        var operation = migration.UpOperations.OfType<SqlOperation>().Single();

        // Assert
        operation.SuppressTransaction.Should().BeTrue();
        operation.Sql.Should().Contain("UPDATE TOP (1000)").And.Contain("#DeletedForms");
    }

    [Fact]
    public void Up_OnPostgreSql_RunsOneStatementInTheMigrationsTransaction()
    {
        // Arrange
        var migration = new PostgreSqlMigration();

        // Act
        var operation = migration.UpOperations.OfType<SqlOperation>().Single();

        // Assert
        operation.SuppressTransaction.Should().BeFalse();
    }
}
