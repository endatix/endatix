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
            CreatesOneObject(statement)
            && (statement.Contains("CREATE TABLE IF NOT EXISTS") || statement.Contains("CREATE INDEX IF NOT EXISTS")));
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
    public void SqlServerTables_FromReferencedQuartz_GuardEachCreateByItsOwnExistenceCheck()
    {
        // Arrange — the migration and the store tests re-run these against a schema that may already hold them,
        // so each one must create a single table or index, and only when that object is missing.

        // Act
        var statements = QuartzSchemaScripts.SqlServerTables();

        // Assert
        statements.Should().NotBeEmpty();
        statements.Should().OnlyContain(
            statement => CreatesOneObject(statement) && IsGuardedCreate(SqlLinesOf(statement)),
            "re-running an unguarded create fails with \"There is already an object named …\". If Quartz reworded a " +
            "guard, widen GuardedTable or GuardedIndex; if it dropped one, make QuartzSchemaScripts wrap that create in " +
            "an existence check, then re-pin the hash");
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

    // A whole statement, nothing before the guard or after its END, so SQL added outside the guard fails it too.
    [GeneratedRegex(
        @"\AIF\s+OBJECT_ID\s*\(\s*N'(?<table>jobs\.\w+)'\s*,\s*N'U'\s*\)\s*IS\s+NULL\s+BEGIN\s+" +
        @"CREATE\s+TABLE\s+\k<table>\s*\(.*\)\s*;?\s*END\s*;?\z",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex GuardedTable();

    [GeneratedRegex(
        @"\AIF\s+NOT\s+EXISTS\s*\(\s*SELECT\s+1\s+FROM\s+sys\.indexes\s+WHERE\s+name\s*=\s*'(?<index>\w+)'\s+AND\s+" +
        @"object_id\s*=\s*OBJECT_ID\s*\(\s*'(?<table>jobs\.\w+)'\s*\)\s*\)\s*BEGIN\s+" +
        @"CREATE\s+INDEX\s+\k<index>\s+ON\s+\k<table>\s*\([^;]*\)\s*;?\s*END\s*;?\z",
        RegexOptions.IgnoreCase | RegexOptions.Singleline)]
    private static partial Regex GuardedIndex();

    [GeneratedRegex(@"\bCREATE\s", RegexOptions.IgnoreCase)]
    private static partial Regex CreateKeyword();

    private static bool IsGuardedCreate(string sql) => GuardedTable().IsMatch(sql) || GuardedIndex().IsMatch(sql);

    private static bool CreatesOneObject(string statement) => CreateKeyword().Count(SqlLinesOf(statement)) == 1;

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
        "with this Quartz or because QuartzRegistration.TablePrefix changed. A prefix change needs a jobs migration " +
        "that renames the existing tables and indexes, then a re-pinned hash. For a Quartz change, " +
        $"{provider} databases created before it may keep the old tables: diff {script} between the two " +
        "versions. If the diff changes no table, column, index or constraint, update the pinned hash only; otherwise " +
        $"add an idempotent {provider} jobs migration that upgrades them, then update the hash";

    private static string? InformationalVersion(Assembly assembly) =>
        assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0];
}
