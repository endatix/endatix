using System.Data.Common;
using Microsoft.Data.SqlClient;
using Npgsql;
using Respawn;
using Respawn.Graph;

namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Resets data to a clean baseline using Respawn for PostgreSQL and SQL Server.
/// Respawner graphs are built once per <see cref="TestDatabaseProvider"/> and reused for each reset.
/// </summary>
public sealed class DatabaseCheckpoint
{
    private static readonly string[] _postgresSchemas = ["public", "identity", "agents", "reporting"];

    private static readonly string[] _sqlServerSchemas = ["dbo", "identity", "agents", "reporting"];

    private readonly SemaphoreSlim _init = new(1, 1);
    private readonly Dictionary<TestDatabaseProvider, Respawner> _respawners = [];

    /// <summary>
    /// Resets all tracked tables to an empty state using Respawn.
    /// On first call per provider it lazily initialises the <see cref="Respawner"/>.
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
        if (_respawners.TryGetValue(provider, out var existing))
        {
            return existing;
        }

        await _init.WaitAsync(cancellationToken);
        try
        {
            if (_respawners.TryGetValue(provider, out existing))
            {
                return existing;
            }

            if (!await HasAnyTablesAsync(connection, provider, cancellationToken))
            {
                return null;
            }

            var respawner = await Respawner.CreateAsync(connection, BuildOptions(provider));
            _respawners[provider] = respawner;
            return respawner;
        }
        finally
        {
            _init.Release();
        }
    }

    private static async Task<bool> HasAnyTablesAsync(DbConnection connection, TestDatabaseProvider provider, CancellationToken cancellationToken)
    {
        var schemas = provider switch
        {
            TestDatabaseProvider.PostgreSql => _postgresSchemas,
            TestDatabaseProvider.SqlServer => _sqlServerSchemas,
            _ => throw new ArgumentOutOfRangeException(nameof(provider))
        };

        var quotedSchemas = string.Join(", ", schemas.Select(s => $"'{s}'"));
        var sql = provider == TestDatabaseProvider.PostgreSql
            ? $"SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema IN ({quotedSchemas}))"
            : $"SELECT CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA IN ({quotedSchemas})) THEN 1 ELSE 0 END";

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is bool b ? b : (int?)result == 1;
    }

    private static DbConnection CreateConnection(string connectionString, TestDatabaseProvider provider) =>
        provider switch
        {
            TestDatabaseProvider.PostgreSql => new NpgsqlConnection(connectionString),
            TestDatabaseProvider.SqlServer => new SqlConnection(connectionString),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported test database provider.")
        };

    private static RespawnerOptions BuildOptions(TestDatabaseProvider provider)
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
            TablesToIgnore = migrationsHistoryTables
        };
    }
}
