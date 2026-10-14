using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Endatix.Modules.Jobs.Persistence;

namespace Endatix.Modules.Jobs.Tests.Persistence;

public sealed partial class QuartzSchemaScriptsTests
{
    // The Quartz version the hashes below were taken from. Existing databases have the tables of that version, and
    // a later Quartz whose statements hash the same needs no jobs migration for that provider.
    private const string PinnedQuartzVersion = "4.2.2";
    private const string PinnedPostgreSqlTablesSha256 = "00070af85c9032af0a502de01a06bf015ac1c069696bc1674ea12f42970902d1";
    private const string PinnedSqlServerTablesSha256 = "c7a39f85a2c80393ab40e2f89557e99354ea33221cc367b05ae68cd20dddede8";

    [Fact]
    public void PinnedQuartzVersion_FromReferencedQuartz_IsTheReferencedVersion()
    {
        // Arrange
        var quartz = typeof(Quartz.IScheduler).Assembly;

        // Act
        var referenced = InformationalVersion(quartz);

        // Assert
        referenced.Should().Be(PinnedQuartzVersion,
            $"the pinned hashes were taken from Quartz {PinnedQuartzVersion} and this build references Quartz " +
            $"{referenced}. Re-check both pinned hashes against Quartz {referenced}, then update PinnedQuartzVersion");
    }

    [Fact]
    public void PostgreSqlTables_FromReferencedQuartz_MatchThePinnedHash()
    {
        // Arrange
        var quartz = typeof(Quartz.IScheduler).Assembly;

        // Act
        var hash = Sha256OfStatements(QuartzSchemaScripts.PostgreSqlTables());

        // Assert
        hash.Should().Be(PinnedPostgreSqlTablesSha256, SchemaChangedReason(quartz, "PostgreSQL", "create_postgres.sql"));
    }

    [Fact]
    public void SqlServerTables_FromReferencedQuartz_MatchThePinnedHash()
    {
        // Arrange
        var quartz = typeof(Quartz.IScheduler).Assembly;

        // Act
        var hash = Sha256OfStatements(QuartzSchemaScripts.SqlServerTables());

        // Assert
        hash.Should().Be(PinnedSqlServerTablesSha256, SchemaChangedReason(quartz, "SQL Server", "create_sqlServer.sql"));
    }

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

    // The statements the migration runs, one batch each, joined by a separator line, so a batch boundary that moves
    // changes the hash. Comment lines, blank lines and indentation are left out, so a change to them alone does not.
    private static string Sha256OfStatements(IEnumerable<string> statements) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(
            string.Join("\n--;;\n", statements.Select(SqlLinesOf)))));

    private static string SqlLinesOf(string statement) =>
        string.Join('\n', statement.Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith("--", StringComparison.Ordinal)));

    private static string SchemaChangedReason(Assembly quartz, string provider, string script) =>
        $"the hash was pinned from Quartz {PinnedQuartzVersion} and this build references Quartz " +
        $"{InformationalVersion(quartz)}. A different hash means the SQL the {provider} jobs migration runs changed, " +
        $"so {provider} databases created before this upgrade may keep the old tables: diff {script} between the two " +
        "versions. If the diff changes no table, column, index or constraint, update the pinned hash only; otherwise " +
        $"add an idempotent {provider} jobs migration that upgrades them, then update the hash";

    private static string? InformationalVersion(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0];
}
