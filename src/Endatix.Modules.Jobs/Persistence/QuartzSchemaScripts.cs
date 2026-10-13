using System.Text;
using System.Text.RegularExpressions;
using Endatix.Modules.Jobs.Runtime;

namespace Endatix.Modules.Jobs.Persistence;

/// <summary>
/// The scheduler's DDL, taken from the Quartz.NET assembly the module references, so the tables a migration
/// creates are always the ones that version of Quartz validates at startup.
/// </summary>
internal static class QuartzSchemaScripts
{
    /// <summary>
    /// The scripts Quartz's own <c>SchemaProvisioning.CreateIfMissing</c> runs. The module keeps provisioning on
    /// <c>Validate</c> and runs them from a migration instead, so deployments that turn automatic migrations
    /// off never have tables created at startup.
    /// </summary>
    private const string PostgreSqlResourceName = "Quartz.Impl.AdoJobStore.Schema.create_postgres.sql";

    private const string SqlServerResourceName = "Quartz.Impl.AdoJobStore.Schema.create_sqlServer.sql";

    /// <summary>Quartz separates the script's statements with a line reading exactly this.</summary>
    private const string StatementSeparator = "--;;";

    /// <summary>
    /// Quartz's PostgreSQL tables under <see cref="QuartzRegistration.TablePrefix"/>, one statement per entry.
    /// Every statement creates only what is missing, so re-running them against an existing schema is harmless.
    /// </summary>
    public static IReadOnlyList<string> PostgreSqlTables() => StatementsOf(PostgreSqlResourceName);

    /// <summary>
    /// The unqualified names of the tables <see cref="PostgreSqlTables"/> creates, read from the script so they
    /// always match the referenced Quartz version.
    /// </summary>
    public static IReadOnlyList<string> PostgreSqlTableNames() => TableNamesIn(PostgreSqlTables());

    /// <summary>
    /// Quartz's SQL Server tables under <see cref="QuartzRegistration.TablePrefix"/>, one statement per entry.
    /// Every statement is guarded by an existence check, so re-running them against an existing schema is
    /// harmless.
    /// </summary>
    public static IReadOnlyList<string> SqlServerTables() => StatementsOf(SqlServerResourceName);

    /// <summary>
    /// The unqualified names of the tables <see cref="SqlServerTables"/> creates, in creation order, read from the
    /// script so they always match the referenced Quartz version. A table is created after the tables its
    /// foreign keys reference, so dropping in reverse order needs no foreign key dropped first.
    /// </summary>
    public static IReadOnlyList<string> SqlServerTableNames() => TableNamesIn(SqlServerTables());

    private static List<string> StatementsOf(string resourceName) =>
        SplitIntoStatements(ReadScript(resourceName));

    private static string ReadScript(string resourceName)
    {
        var assembly = typeof(Quartz.IScheduler).Assembly;
        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Quartz.NET no longer embeds '{resourceName}'. Check the scheduler's schema scripts " +
                "after upgrading Quartz and update the jobs migrations to match.");
        using var reader = new StreamReader(stream);

        // '{0}' is the full prefix and '{1}' the prefix without its schema, for index and constraint names,
        // which both providers place in their table's schema anyway.
        return reader.ReadToEnd()
            .Replace("{0}", QuartzRegistration.TablePrefix, StringComparison.Ordinal)
            .Replace("{1}", UnqualifiedPrefix, StringComparison.Ordinal);
    }

    // The separator is a whole line: the script's own header mentions it mid-sentence.
    private static List<string> SplitIntoStatements(string script)
    {
        var statements = new List<string>();
        var current = new StringBuilder();
        foreach (var line in script.ReplaceLineEndings("\n").Split('\n'))
        {
            if (line.Trim() == StatementSeparator)
            {
                AddIfSql(statements, current);
                continue;
            }

            current.Append(line).Append('\n');
        }

        AddIfSql(statements, current);
        return statements;
    }

    private static string[] TableNamesIn(IEnumerable<string> statements) =>
        statements
            .Select(statement => CreateTable.Match(statement))
            .Where(match => match.Success)
            .Select(match => match.Groups["table"].Value)
            .ToArray();

    private static void AddIfSql(List<string> statements, StringBuilder current)
    {
        var statement = current.ToString().Trim();
        current.Clear();
        if (ContainsSql(statement))
        {
            statements.Add(statement);
        }
    }

    // The input is Quartz's own script, not user input, but a bound keeps any regex from running unchecked.
    private static readonly Regex CreateTable = new(
        $@"CREATE TABLE (?:IF NOT EXISTS )?{Regex.Escape(SchemaName)}\.(?<table>\w+)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromSeconds(1));

    private static string SchemaName =>
        QuartzRegistration.TablePrefix[..QuartzRegistration.TablePrefix.LastIndexOf('.')];

    private static string UnqualifiedPrefix =>
        QuartzRegistration.TablePrefix[(QuartzRegistration.TablePrefix.LastIndexOf('.') + 1)..];

    private static bool ContainsSql(string statement) =>
        statement.Split('\n').Any(line => line.Trim() is { Length: > 0 } text && !text.StartsWith("--", StringComparison.Ordinal));
}
