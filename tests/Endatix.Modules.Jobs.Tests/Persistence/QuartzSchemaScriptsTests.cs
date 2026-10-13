using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Endatix.Modules.Jobs.Persistence;

namespace Endatix.Modules.Jobs.Tests.Persistence;

public sealed partial class QuartzSchemaScriptsTests
{
    // The Quartz version the hashes below were taken from. Existing databases have the tables of that version, and
    // a later Quartz whose script hashes the same needs no jobs migration for that provider.
    private const string PinnedQuartzVersion = "4.2.2";
    private const string PinnedPostgreSqlScriptSha256 = "2cee704d332085e3fed27d0f571405abfd9e6989ead422018243ab2e2485ad7d";
    private const string PinnedSqlServerScriptSha256 = "027d36c80d89f6f059d03b14b6e5c5f98b3741b05d668de938e2b2041a23b8bf";

    [Fact]
    public void PostgreSqlScript_FromReferencedQuartz_MatchesThePinnedHash()
    {
        // Arrange
        var quartz = typeof(Quartz.IScheduler).Assembly;

        // Act
        var hash = Sha256OfScriptAsTheMigrationReadsIt(QuartzSchemaScripts.PostgreSqlResourceName);

        // Assert
        hash.Should().Be(PinnedPostgreSqlScriptSha256, SchemaChangedReason(quartz, "PostgreSQL", QuartzSchemaScripts.PostgreSqlResourceName));
    }

    [Fact]
    public void SqlServerScript_FromReferencedQuartz_MatchesThePinnedHash()
    {
        // Arrange
        var quartz = typeof(Quartz.IScheduler).Assembly;

        // Act
        var hash = Sha256OfScriptAsTheMigrationReadsIt(QuartzSchemaScripts.SqlServerResourceName);

        // Assert
        hash.Should().Be(PinnedSqlServerScriptSha256, SchemaChangedReason(quartz, "SQL Server", QuartzSchemaScripts.SqlServerResourceName));
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

    private static string Sha256OfScriptAsTheMigrationReadsIt(string resource) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(QuartzSchemaScripts.ScriptText(resource))));

    private static string SchemaChangedReason(Assembly quartz, string provider, string resource) =>
        $"the hash was pinned from Quartz {PinnedQuartzVersion} and this build references Quartz " +
        $"{InformationalVersion(quartz)}. Diff {resource} between the two versions. If only comments or whitespace " +
        "changed, update the pinned hash and the Quartz version beside it, and nothing else. Otherwise the Quartz " +
        $"{provider} schema changed with this upgrade: {provider} databases created before it keep the old tables, so " +
        $"add an idempotent {provider} jobs migration that upgrades them, then update the pinned hash and the version";

    private static string? InformationalVersion(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0];
}
