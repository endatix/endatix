using Endatix.Modules.Jobs.Persistence;

namespace Endatix.Modules.Jobs.Tests.Persistence;

public sealed class QuartzSchemaScriptsTests
{
    [Fact]
    public void PostgreSqlTables_FromReferencedQuartz_PlacesEveryTableInJobsSchema()
    {
        // Arrange

        // Act
        var statements = QuartzSchemaScripts.PostgreSqlTables();

        // Assert
        var script = string.Join('\n', statements);
        script.Should().NotContain("{0}").And.NotContain("{1}");
        statements.Where(statement => statement.Contains("CREATE TABLE"))
            .Should().OnlyContain(statement => statement.Contains("CREATE TABLE IF NOT EXISTS jobs.qrtz_"));
    }

    [Fact]
    public void PostgreSqlTableNames_FromReferencedQuartz_ListsTheSchedulerTables()
    {
        // Arrange — the tables the scheduler cannot run without.
        string[] coreTables = ["qrtz_job_details", "qrtz_triggers", "qrtz_fired_triggers", "qrtz_locks", "qrtz_scheduler_state"];

        // Act
        var tables = QuartzSchemaScripts.PostgreSqlTableNames();

        // Assert
        tables.Should().Contain(coreTables);
        tables.Should().OnlyContain(table => table.StartsWith("qrtz_"));
        tables.Should().OnlyHaveUniqueItems();
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
