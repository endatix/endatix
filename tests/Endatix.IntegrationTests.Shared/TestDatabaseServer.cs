using System.Data.Common;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// The statements that create, copy and drop whole databases on the test container. They run on the server connection,
/// the container's own empty database, never on the database they change.
/// </summary>
internal sealed class TestDatabaseServer(TestDatabaseProvider provider, string serverConnectionString)
{
    /// <summary>Names every database a fixture gets, so a reused container's leftovers can be found.</summary>
    public const string FixturePrefix = "endatix_fx_";

    public const string TemplatePrefix = "endatix_template_";

    private const string CreatedFormat = "yyMMddHHmm";

    // The template backup sits beside the database files of the Linux SQL Server image.
    private const string SqlServerBackupFolder = "/var/opt/mssql/data";

    public TestDatabaseProvider Provider { get; } = provider;

    public string ServerConnectionString { get; } = serverConnectionString;

    /// <summary>A new database name: the prefix, when it was made (UTC), and a random part.</summary>
    public static string NewName(string prefix, DateTime utcNow) =>
        $"{prefix}{utcNow.ToString(CreatedFormat, CultureInfo.InvariantCulture)}_{Guid.NewGuid().ToString("N")[..8]}";

    /// <summary>
    /// Whether <paramref name="name"/> was made before <paramref name="cutoff"/>. A name without a creation time, from
    /// before the time was in it, counts as old.
    /// </summary>
    public static bool MadeBefore(string name, DateTime cutoff)
    {
        var prefix = name.StartsWith(FixturePrefix, StringComparison.Ordinal) ? FixturePrefix : TemplatePrefix;
        var stamp = name.Length >= prefix.Length + CreatedFormat.Length ? name.Substring(prefix.Length, CreatedFormat.Length) : string.Empty;
        return !DateTime.TryParseExact(stamp, CreatedFormat, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var made)
            || made < cutoff;
    }

    public string ConnectionStringFor(string database) => Provider switch
    {
        TestDatabaseProvider.PostgreSql => new NpgsqlConnectionStringBuilder(ServerConnectionString) { Database = database }.ConnectionString,
        TestDatabaseProvider.SqlServer => new SqlConnectionStringBuilder(ServerConnectionString) { InitialCatalog = database }.ConnectionString,
        _ => throw Unsupported(Provider),
    };

    public Task CreateEmptyAsync(string name, CancellationToken cancellationToken) =>
        ExecuteAsync(Provider switch
        {
            TestDatabaseProvider.PostgreSql => $"CREATE DATABASE {name}",
            TestDatabaseProvider.SqlServer => $"CREATE DATABASE [{name}]",
            _ => throw Unsupported(Provider),
        }, cancellationToken);

    /// <summary>
    /// Stops the template changing and makes it copyable. PostgreSQL refuses connections to it from here on, so no
    /// session can block a copy; SQL Server backs it up once, to restore from.
    /// </summary>
    public Task SealTemplateAsync(string template, CancellationToken cancellationToken) =>
        ExecuteAsync(Provider switch
        {
            TestDatabaseProvider.PostgreSql => $"""
                ALTER DATABASE {template} WITH ALLOW_CONNECTIONS false IS_TEMPLATE true;
                SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{template}' AND pid <> pg_backend_pid();
                """,
            TestDatabaseProvider.SqlServer => $"BACKUP DATABASE [{template}] TO DISK = N'{BackupPath(template)}' WITH INIT, COPY_ONLY",
            _ => throw Unsupported(Provider),
        }, cancellationToken);

    public async Task CopyTemplateAsync(string template, string name, CancellationToken cancellationToken)
    {
        switch (Provider)
        {
            case TestDatabaseProvider.PostgreSql:
                await ExecuteAsync($"CREATE DATABASE {name} TEMPLATE {template}", cancellationToken);
                break;
            case TestDatabaseProvider.SqlServer:
                await RestoreAsync(template, name, cancellationToken);
                break;
            default:
                throw Unsupported(Provider);
        }
    }

    // A restored database needs files of its own, so each logical file of the backup is moved to a name of its own.
    private async Task RestoreAsync(string template, string name, CancellationToken cancellationToken)
    {
        var files = await QueryAsync(
            $"SELECT name, physical_name FROM sys.master_files WHERE database_id = DB_ID(N'{template}')",
            reader => (Logical: reader.GetString(0), Physical: reader.GetString(1)),
            cancellationToken);
        var moves = files.Select(file =>
        {
            var folder = file.Physical[..(file.Physical.LastIndexOf('/') + 1)];
            var extension = Path.GetExtension(file.Physical);
            return $"MOVE N'{file.Logical}' TO N'{folder}{name}_{file.Logical}{extension}'";
        });
        await ExecuteAsync(
            $"RESTORE DATABASE [{name}] FROM DISK = N'{BackupPath(template)}' WITH {string.Join(", ", moves)}",
            cancellationToken);
    }

    /// <summary>
    /// Drops the database, ending every session on it first, pooled ones included. Pools are not cleared: clearing is
    /// process-wide and would cost other fixtures' hosts their connections, and a pooled connection to a dropped database
    /// is never asked for again.
    /// </summary>
    public Task DropAsync(string name, CancellationToken cancellationToken) =>
        ExecuteAsync(Provider switch
        {
            TestDatabaseProvider.PostgreSql => $"DROP DATABASE IF EXISTS {name} WITH (FORCE)",
            TestDatabaseProvider.SqlServer => $"""
                IF DB_ID(N'{name}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{name}];
                END
                """,
            _ => throw Unsupported(Provider),
        }, cancellationToken);

    /// <summary>The fixture and template databases an earlier run on a reused container left behind.</summary>
    public Task<List<string>> LeftoversAsync(CancellationToken cancellationToken) =>
        QueryAsync(
            Provider switch
            {
                TestDatabaseProvider.PostgreSql =>
                    $"SELECT datname FROM pg_database WHERE datname LIKE '{FixturePrefix}%' OR datname LIKE '{TemplatePrefix}%'",
                TestDatabaseProvider.SqlServer =>
                    $"SELECT name FROM sys.databases WHERE name LIKE '{Escaped(FixturePrefix)}%' OR name LIKE '{Escaped(TemplatePrefix)}%'",
                _ => throw Unsupported(Provider),
            },
            reader => reader.GetString(0),
            cancellationToken);

    /// <summary>Lets a template from an earlier run be dropped: PostgreSQL refuses to drop a database marked as one.</summary>
    public Task UnmarkTemplateAsync(string name, CancellationToken cancellationToken) =>
        Provider == TestDatabaseProvider.PostgreSql
            ? ExecuteAsync($"ALTER DATABASE {name} WITH IS_TEMPLATE false", cancellationToken)
            : Task.CompletedTask;

    private static string Escaped(string likePattern) => likePattern.Replace("_", "[_]", StringComparison.Ordinal);

    private static string BackupPath(string template) => $"{SqlServerBackupFolder}/{template}.bak";

    private async Task ExecuteAsync(string sql, CancellationToken cancellationToken)
    {
        await using var connection = Connect();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 120;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<List<T>> QueryAsync<T>(string sql, Func<DbDataReader, T> read, CancellationToken cancellationToken)
    {
        await using var connection = Connect();
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        List<T> rows = [];
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(read(reader));
        }

        return rows;
    }

    private DbConnection Connect() => Provider switch
    {
        TestDatabaseProvider.PostgreSql => new NpgsqlConnection(ServerConnectionString),
        TestDatabaseProvider.SqlServer => new SqlConnection(ServerConnectionString),
        _ => throw Unsupported(Provider),
    };

    private static ArgumentOutOfRangeException Unsupported(TestDatabaseProvider provider) =>
        new(nameof(provider), provider, "Unsupported test database provider.");
}
