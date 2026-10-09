using System.Text.RegularExpressions;
using Endatix.Modules.Jobs.Persistence;

namespace Endatix.Modules.Jobs.Tests.Persistence;

public sealed partial class QuartzSchemaScriptsTests
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

    [Fact]
    public void SqlServerTables_FromReferencedQuartz_PlacesEveryTableInJobsSchema()
    {
        // Arrange

        // Act
        var statements = QuartzSchemaScripts.SqlServerTables();

        // Assert
        var script = string.Join('\n', statements);
        script.Should().NotContain("{0}").And.NotContain("{1}");
        statements.Where(statement => statement.Contains("CREATE TABLE"))
            .Should().OnlyContain(statement => statement.Contains("CREATE TABLE jobs.qrtz_"));
    }

    [Fact]
    public void SqlServerTables_FromReferencedQuartz_NamesIndexesWithoutTheSchema()
    {
        // Arrange — constraint and index names cannot carry a schema; SQL Server places them in their table's.

        // Act
        var statements = QuartzSchemaScripts.SqlServerTables();

        // Assert
        var names = statements.SelectMany(statement => ConstraintOrIndexName().Matches(statement))
            .Select(match => match.Groups["name"].Value)
            .ToList();
        names.Should().NotBeEmpty();
        names.Should().OnlyContain(name => name.Contains("_qrtz_") && !name.Contains('.'));
        statements.Should().Contain(statement =>
            statement.Contains("CREATE INDEX IDX_qrtz_T_NFT_ST ON jobs.qrtz_TRIGGERS"));
    }

    [Fact]
    public void SqlServerTables_FromReferencedQuartz_ContainNoBatchSeparator()
    {
        // Arrange — each entry runs as one command, which a GO line would break.

        // Act
        var statements = QuartzSchemaScripts.SqlServerTables();

        // Assert
        statements.Should().NotBeEmpty();
        statements.SelectMany(statement => statement.Split('\n'))
            .Should().NotContain(line => line.Trim().Equals("GO", StringComparison.OrdinalIgnoreCase));
        statements.Should().NotContain(statement => statement.Contains("--;;"));
    }

    [Fact]
    public void SqlServerTableNames_FromReferencedQuartz_ListsTheSchedulerTables()
    {
        // Arrange — the tables the scheduler cannot run without.
        string[] coreTables = ["qrtz_JOB_DETAILS", "qrtz_TRIGGERS", "qrtz_FIRED_TRIGGERS", "qrtz_LOCKS", "qrtz_SCHEDULER_STATE"];

        // Act
        var tables = QuartzSchemaScripts.SqlServerTableNames();

        // Assert
        tables.Should().Contain(coreTables);
        tables.Should().OnlyContain(table => table.StartsWith("qrtz_"));
        tables.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public void SqlServerTableNames_FromReferencedQuartz_ListReferencedTablesFirst()
    {
        // Arrange — the SQL Server migration drops the tables in reverse of this order, without dropping foreign keys.
        var statements = QuartzSchemaScripts.SqlServerTables();

        // Act
        var tables = QuartzSchemaScripts.SqlServerTableNames().ToList();

        // Assert
        var references = statements
            .Select(statement => (Table: CreatedTable().Match(statement), Referenced: ReferencedTable().Matches(statement)))
            .Where(statement => statement.Table.Success)
            .SelectMany(statement => statement.Referenced.Select(referenced =>
                (Table: statement.Table.Groups["table"].Value, Referenced: referenced.Groups["table"].Value)))
            .ToList();
        references.Should().NotBeEmpty();
        references.Should().OnlyContain(reference =>
            tables.IndexOf(reference.Referenced) < tables.IndexOf(reference.Table));
    }

    [Fact]
    public void JobsAssembly_ManifestResources_EmbedNoCreateScript()
    {
        // Arrange — the scheduler's DDL is read from the referenced Quartz assembly, never copied into the module.
        var assembly = typeof(QuartzSchemaScripts).Assembly;

        // Act
        var resources = assembly.GetManifestResourceNames();

        // Assert
        resources.Should().NotContain(resource =>
            resource.Contains("create_", StringComparison.OrdinalIgnoreCase)
            && resource.EndsWith(".sql", StringComparison.OrdinalIgnoreCase));
    }

    [GeneratedRegex(@"(?:CONSTRAINT|CREATE INDEX)\s+(?<name>[\w.]+)")]
    private static partial Regex ConstraintOrIndexName();

    [GeneratedRegex(@"CREATE TABLE jobs\.(?<table>\w+)")]
    private static partial Regex CreatedTable();

    [GeneratedRegex(@"REFERENCES jobs\.(?<table>\w+)")]
    private static partial Regex ReferencedTable();
}
