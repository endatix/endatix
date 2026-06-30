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

            try
            {
                var respawner = await Respawner.CreateAsync(connection, BuildOptions(provider));
                _respawners[provider] = respawner;
                return respawner;
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("No tables found", StringComparison.Ordinal))
            {
                // Fresh database before migrations; nothing to reset yet.
                return null;
            }
        }
        finally
        {
            _init.Release();
        }
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
