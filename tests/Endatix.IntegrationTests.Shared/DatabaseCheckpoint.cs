using System.Data.Common;
using Microsoft.Data.SqlClient;
using Npgsql;
using Respawn;

namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Resets data to a clean baseline using Respawn for PostgreSQL and SQL Server.
/// </summary>
public sealed class DatabaseCheckpoint
{
    private readonly SemaphoreSlim _init = new(1, 1);
    private readonly Dictionary<TestDatabaseProvider, Respawner> _respawners = [];

    public async Task ResetAsync(
        string connectionString,
        TestDatabaseProvider provider,
        CancellationToken cancellationToken = default)
    {
        await using var connection = CreateConnection(connectionString, provider);
        await connection.OpenAsync(cancellationToken);

        if (!_respawners.ContainsKey(provider))
        {
            await _init.WaitAsync(cancellationToken);
            try
            {
                if (!_respawners.ContainsKey(provider))
                {
                    var respawner = await Respawner.CreateAsync(connection, BuildOptions(provider));
                    _respawners[provider] = respawner;
                }
            }
            finally
            {
                _init.Release();
            }
        }

        await _respawners[provider].ResetAsync(connection);
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
        switch (provider)
        {
            case TestDatabaseProvider.PostgreSql:
                return new RespawnerOptions
                {
                    DbAdapter = DbAdapter.Postgres,
                    SchemasToInclude = ["public", "identity", "agents"],
                    TablesToIgnore = ["__EFMigrationsHistory"]
                };
            case TestDatabaseProvider.SqlServer:
                return new RespawnerOptions
                {
                    DbAdapter = DbAdapter.SqlServer,
                    SchemasToInclude = ["dbo", "identity", "agents"],
                    TablesToIgnore = ["__EFMigrationsHistory"]
                };
            default:
                throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported test database provider.");
        }
    }
}
