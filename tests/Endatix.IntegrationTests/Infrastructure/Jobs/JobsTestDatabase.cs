using Npgsql;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>
/// A database of its own on the shared test server, so scheduler nodes started by a test share a store with
/// each other and with nothing else — the web host's scheduler never sees their triggers.
/// </summary>
internal sealed class JobsTestDatabase : IAsyncDisposable
{
    private readonly string _serverConnectionString;
    private readonly string _name;

    private JobsTestDatabase(string serverConnectionString, string name, string connectionString)
    {
        _serverConnectionString = serverConnectionString;
        _name = name;
        ConnectionString = connectionString;
    }

    public string ConnectionString { get; }

    public static async Task<JobsTestDatabase> CreateAsync(
        string serverConnectionString,
        CancellationToken cancellationToken)
    {
        var name = $"jobs_{Guid.NewGuid():N}"[..20];
        await ExecuteAsync(serverConnectionString, $"CREATE DATABASE {name}", cancellationToken);

        var connectionString = new NpgsqlConnectionStringBuilder(serverConnectionString) { Database = name }
            .ConnectionString;
        return new JobsTestDatabase(serverConnectionString, name, connectionString);
    }

    public Task ExecuteAsync(string sql, CancellationToken cancellationToken) =>
        ExecuteAsync(ConnectionString, sql, cancellationToken);

    public async Task<long> CountAsync(string sql, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        return Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken));
    }

    public async Task<List<T>> QueryAsync<T>(
        string sql,
        Func<NpgsqlDataReader, T> read,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
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
        NpgsqlConnection.ClearAllPools();
        await ExecuteAsync(_serverConnectionString, $"DROP DATABASE IF EXISTS {_name} WITH (FORCE)", CancellationToken.None);
    }

    private static async Task ExecuteAsync(string connectionString, string sql, CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
