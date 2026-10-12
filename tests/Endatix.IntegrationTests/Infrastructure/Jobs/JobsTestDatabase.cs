using System.Data.Common;
using Endatix.IntegrationTests.Shared;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>
/// A database of its own on the shared test server, so scheduler nodes started by a test share a store with
/// each other and with nothing else — the web host's scheduler never sees their triggers.
/// </summary>
internal sealed class JobsTestDatabase : IAsyncDisposable
{
    private readonly ConnectionTarget _server;
    private readonly ConnectionTarget _database;
    private readonly string _name;

    private JobsTestDatabase(ConnectionTarget server, string name)
    {
        _server = server;
        _name = name;
        _database = server.ForDatabase(name);
    }

    public string ConnectionString => _database.ConnectionString;

    /// <summary>Creates a PostgreSQL database on the server <paramref name="serverConnectionString"/> points at.</summary>
    public static Task<JobsTestDatabase> CreateAsync(string serverConnectionString, CancellationToken cancellationToken) =>
        CreateAsync(TestDatabaseProvider.PostgreSql, serverConnectionString, cancellationToken);

    /// <summary>Creates a database of <paramref name="provider"/> on the server <paramref name="serverConnectionString"/> points at.</summary>
    public static async Task<JobsTestDatabase> CreateAsync(
        TestDatabaseProvider provider,
        string serverConnectionString,
        CancellationToken cancellationToken)
    {
        var server = new ConnectionTarget(provider, serverConnectionString);
        var name = $"jobs_{Guid.NewGuid():N}"[..20];
        await server.ExecuteAsync($"CREATE DATABASE {name}", cancellationToken);
        return new JobsTestDatabase(server, name);
    }

    public Task ExecuteAsync(string sql, CancellationToken cancellationToken) =>
        _database.ExecuteAsync(sql, cancellationToken);

    public async Task<long> CountAsync(string sql, CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = ConnectionTarget.CommandFor(connection, sql);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<List<T>> QueryAsync<T>(
        string sql,
        Func<DbDataReader, T> read,
        CancellationToken cancellationToken)
    {
        await using var connection = await _database.OpenAsync(cancellationToken);
        await using var command = ConnectionTarget.CommandFor(connection, sql);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        List<T> rows = [];
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(read(reader));
        }

        return rows;
    }

    public async ValueTask DisposeAsync()
    {
        // Pooled connections hold the database open, and both providers refuse to drop a database in use.
        NpgsqlConnection.ClearAllPools();
        SqlConnection.ClearAllPools();
        await _server.ExecuteAsync(DropDatabaseSql(_server.Provider, _name), CancellationToken.None);
    }

    private static string DropDatabaseSql(TestDatabaseProvider provider, string name) => provider switch
    {
        TestDatabaseProvider.PostgreSql => $"DROP DATABASE IF EXISTS {name} WITH (FORCE)",
        TestDatabaseProvider.SqlServer => $"""
            IF DB_ID(N'{name}') IS NOT NULL
            BEGIN
                ALTER DATABASE [{name}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                DROP DATABASE [{name}];
            END
            """,
        _ => throw ConnectionTarget.Unsupported(provider),
    };

    /// <summary>A connection string and the provider that reads it, which every statement here needs together.</summary>
    private sealed record ConnectionTarget(TestDatabaseProvider Provider, string ConnectionString)
    {
        public ConnectionTarget ForDatabase(string name) => this with
        {
            ConnectionString = Provider switch
            {
                TestDatabaseProvider.PostgreSql =>
                    new NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ConnectionString,
                TestDatabaseProvider.SqlServer =>
                    new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = name }.ConnectionString,
                _ => throw Unsupported(Provider),
            },
        };

        public async Task ExecuteAsync(string sql, CancellationToken cancellationToken)
        {
            await using var connection = await OpenAsync(cancellationToken);
            await using var command = CommandFor(connection, sql);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken)
        {
            DbConnection connection = Provider switch
            {
                TestDatabaseProvider.PostgreSql => new NpgsqlConnection(ConnectionString),
                TestDatabaseProvider.SqlServer => new SqlConnection(ConnectionString),
                _ => throw Unsupported(Provider),
            };
            try
            {
                await connection.OpenAsync(cancellationToken);
                return connection;
            }
            catch
            {
                await connection.DisposeAsync();
                throw;
            }
        }

        public static DbCommand CommandFor(DbConnection connection, string sql)
        {
            var command = connection.CreateCommand();
            command.CommandText = sql;
            return command;
        }

        public static ArgumentOutOfRangeException Unsupported(TestDatabaseProvider provider) =>
            new(nameof(provider), provider, "Unsupported test database provider.");
    }
}
