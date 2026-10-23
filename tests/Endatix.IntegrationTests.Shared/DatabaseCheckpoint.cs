using System.Data.Common;
using Microsoft.Data.SqlClient;
using Npgsql;
using Respawn;
using Respawn.Graph;

namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Resets data to a clean baseline using Respawn for PostgreSQL and SQL Server.
/// Respawner graphs are built per <see cref="TestDatabaseProvider"/> and reused until the set of tables changes.
/// </summary>
public sealed class DatabaseCheckpoint
{
    private static readonly string[] _postgresSchemas = ["public", "identity", "agents", "reporting", "jobs", "audience"];

    private static readonly string[] _sqlServerSchemas = ["dbo", "identity", "agents", "reporting", "jobs", "audience"];

    private readonly SemaphoreSlim _init = new(1, 1);
    private readonly Dictionary<TestDatabaseProvider, (Respawner Respawner, string Tables)> _respawners = [];

    /// <summary>
    /// Resets all tracked tables to an empty state using Respawn.
    /// The <see cref="Respawner"/> is built lazily and rebuilt whenever the set of tracked tables changes,
    /// so a reset that ran before a migration does not freeze a graph without the migrated tables.
    /// </summary>
    public async Task ResetAsync(
        string connectionString,
        TestDatabaseProvider provider,
        CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection(connectionString, provider);
        await connection.OpenAsync(cancellationToken);

        var respawner = await GetOrCreateRespawnerAsync(connection, provider, cancellationToken);
        if (respawner is null)
        {
            return;
        }

        await respawner.ResetAsync(connection);
    }

    private async Task<Respawner?> GetOrCreateRespawnerAsync(
        DbConnection connection,
        TestDatabaseProvider provider,
        CancellationToken cancellationToken)
    {
        await _init.WaitAsync(cancellationToken);
        try
        {
            var tables = await ResettableTablesAsync(connection, provider, cancellationToken);
            if (tables.Length == 0)
            {
                return null;
            }

            if (_respawners.TryGetValue(provider, out var existing) && existing.Tables == tables)
            {
                return existing.Respawner;
            }

            try
            {
                var respawner = await Respawner.CreateAsync(
                    connection,
                    BuildOptions(provider, await KeptTablesAsync(connection, provider, cancellationToken)));
                _respawners[provider] = (respawner, tables);
                return respawner;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("No tables found", StringComparison.Ordinal))
            {
                return null;
            }
        }
        finally
        {
            _init.Release();
        }
    }

    // SQL Server's own tables are left out: the Testcontainers connection string points at master, whose dbo
    // schema ships with tables such as spt_monitor before any migration has run.
    private static async Task<string> ResettableTablesAsync(
        DbConnection connection,
        TestDatabaseProvider provider,
        CancellationToken cancellationToken)
    {
        var sql = provider switch
        {
            TestDatabaseProvider.PostgreSql => $"""
                SELECT table_schema || '.' || table_name
                FROM information_schema.tables
                WHERE table_type = 'BASE TABLE'
                  AND table_schema IN ({QuotedSchemas(_postgresSchemas)})
                  AND table_name <> '__EFMigrationsHistory'
                ORDER BY 1
                """,
            TestDatabaseProvider.SqlServer => $"""
                SELECT SCHEMA_NAME(schema_id) + '.' + name
                FROM sys.tables
                WHERE is_ms_shipped = 0
                  AND SCHEMA_NAME(schema_id) IN ({QuotedSchemas(_sqlServerSchemas)})
                  AND name <> '__EFMigrationsHistory'
                ORDER BY 1
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported test database provider.")
        };

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var tables = new List<string>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(reader.GetString(0));
        }

        return string.Join(',', tables);
    }

    private static string QuotedSchemas(string[] schemas) => string.Join(", ", schemas.Select(s => $"'{s}'"));

    private static DbConnection CreateConnection(string connectionString, TestDatabaseProvider provider) =>
        provider switch
        {
            TestDatabaseProvider.PostgreSql => new NpgsqlConnection(connectionString),
            TestDatabaseProvider.SqlServer => new SqlConnection(connectionString),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported test database provider.")
        };

    // Tables a reset leaves alone. The job scheduler's own tables: a running host's scheduler keeps its check-in,
    // locks and durable jobs there, so wiping them under it would break the host rather than reset data; triggers
    // left behind by an earlier test point at job rows that no longer exist and fire into nothing. They are read
    // from the database, because the migration takes them from whichever Quartz version is referenced. On SQL
    // Server also its own shipped tables (master's dbo.spt_*), which are not test data.
    private static async Task<Table[]> KeptTablesAsync(
        DbConnection connection,
        TestDatabaseProvider provider,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = provider switch
        {
            TestDatabaseProvider.PostgreSql => """
                SELECT table_schema, table_name
                FROM information_schema.tables
                WHERE table_schema = 'jobs' AND table_name LIKE 'qrtz\_%'
                """,
            TestDatabaseProvider.SqlServer => """
                SELECT SCHEMA_NAME(schema_id), name
                FROM sys.tables
                WHERE (SCHEMA_NAME(schema_id) = 'jobs' AND name LIKE 'qrtz[_]%')
                   OR is_ms_shipped = 1
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported test database provider.")
        };

        var tables = new List<Table>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(new Table(reader.GetString(0), reader.GetString(1)));
        }

        return [.. tables];
    }

    private static RespawnerOptions BuildOptions(TestDatabaseProvider provider, Table[] keptTables)
    {
        var schemas = provider switch
        {
            TestDatabaseProvider.PostgreSql => _postgresSchemas,
            TestDatabaseProvider.SqlServer => _sqlServerSchemas,
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported test database provider.")
        };

        // Each module schema keeps its own EF migrations history table (see ModuleDbContextExtensions).
        var migrationsHistoryTables = schemas
            .Select(schema => new Table(schema, "__EFMigrationsHistory"))
            .ToArray();

        return new RespawnerOptions
        {
            SchemasToInclude = schemas,
            TablesToIgnore = [.. migrationsHistoryTables, .. keptTables]
        };
    }
}
