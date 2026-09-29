using Endatix.Modules.Jobs.Persistence;

namespace Endatix.Modules.Jobs.Tests.Persistence;

public sealed class QuartzSchemaScriptsTests
{
    [Fact]
    public void PostgreSqlTables_FromReferencedQuartz_PlacesEveryTableInJobsSchema()
    {
        // Arrange
        var expectedTables = QuartzSchemaScripts.TablesInDropOrder.Select(table => $"jobs.{table}");

        // Act
        var statements = QuartzSchemaScripts.PostgreSqlTables();

        // Assert
        var script = string.Join('\n', statements);
        script.Should().NotContain("{0}").And.NotContain("{1}");
        foreach (var table in expectedTables)
        {
            script.Should().Contain($"CREATE TABLE IF NOT EXISTS {table} (");
        }
    }

    [Fact]
    public void PostgreSqlTables_FromReferencedQuartz_NamesIndexesWithoutTheSchema()
    {
        // Arrange — PostgreSQL rejects a schema-qualified index name, and creates the index in its table's schema.

        // Act
        var statements = QuartzSchemaScripts.PostgreSqlTables();

        // Assert
        statements.Should().Contain(statement =>
            statement.Contains("CREATE INDEX IF NOT EXISTS idx_qrtz_t_nft_st ON jobs.qrtz_triggers"));
        statements.Should().NotContain(statement => statement.Contains("idx_jobs."));
    }

    [Fact]
    public void PostgreSqlTables_FromReferencedQuartz_ReturnsOneStatementPerEntry()
    {
        // Arrange — each entry becomes its own migration command.

        // Act
        var statements = QuartzSchemaScripts.PostgreSqlTables();

        // Assert
        statements.Should().NotBeEmpty();
        statements.Should().NotContain(statement => statement.Contains("--;;"));
        statements.Should().OnlyContain(statement =>
            statement.Contains("CREATE TABLE IF NOT EXISTS") || statement.Contains("CREATE INDEX IF NOT EXISTS"));
    }
}
