using System.Data;
using System.Data.Common;
using Endatix.IntegrationTests.Shared;
using Microsoft.Data.SqlClient;

namespace Endatix.IntegrationTests.Infrastructure.Jobs;

/// <summary>
/// A database of its own on the shared test server, so scheduler nodes started by a test share a store with
/// each other and with nothing else — the web host's scheduler never sees their triggers.
/// </summary>
internal sealed class JobsTestDatabase : IAsyncDisposable
{
    private static readonly IReadOnlyDictionary<string, object> NoParameters = new Dictionary<string, object>();

    private readonly ConnectionTarget _server;
    private readonly ConnectionTarget _database;
    private readonly string _name;
    private bool _away;
    private bool _readOnly;

    private JobsTestDatabase(ConnectionTarget server, string name)
    {
        _server = server;
        _name = name;
        _database = server.ForDatabase(name);
    }

    public string ConnectionString => _database.ConnectionString;

    public TestDatabaseProvider Provider => _server.Provider;

    /// <summary>Creates a database of the fixture's provider on the fixture's server.</summary>
    public static Task<JobsTestDatabase> CreateAsync(DbIntegrationFixture fixture, CancellationToken cancellationToken) =>
        CreateAsync(fixture.Provider, fixture.ConnectionString, cancellationToken);

    /// <summary>Creates a database of <paramref name="provider"/> on the server <paramref name="serverConnectionString"/> points at.</summary>
    public static async Task<JobsTestDatabase> CreateAsync(
        TestDatabaseProvider provider,
        string serverConnectionString,
        CancellationToken cancellationToken)
    {
        var server = new ConnectionTarget(provider, serverConnectionString);
        var name = $"jobs_{Guid.NewGuid():N}"[..20];
        await server.ExecuteAsync($"CREATE DATABASE {name}", NoParameters, cancellationToken);
        return new JobsTestDatabase(server, name);
    }

    public Task ExecuteAsync(string sql, CancellationToken cancellationToken) =>
        _database.ExecuteAsync(sql, NoParameters, cancellationToken);

    /// <summary>
    /// Runs <paramref name="sql"/> with each of <paramref name="parameters"/> bound by its name, which the statement
    /// refers to as <c>@name</c> on either provider.
    /// </summary>
    public Task ExecuteAsync(string sql, IReadOnlyDictionary<string, object> parameters, CancellationToken cancellationToken) =>
        _database.ExecuteAsync(sql, parameters, cancellationToken);

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

    /// <summary>
    /// Makes the database impossible to open, as during a failover or a restore, until
    /// <see cref="BringBackAsync"/>: SQL Server takes it offline (4060), PostgreSQL renames it away (3D000).
    /// </summary>
    public async Task TakeAwayAsync(CancellationToken cancellationToken)
    {
        _server.ClearPools();
        await _server.ExecuteAsync(TakeAwaySql(_server.Provider, _name), NoParameters, cancellationToken);
        _away = true;
    }

    /// <summary>Makes the database open again, after <see cref="TakeAwayAsync"/>.</summary>
    public async Task BringBackAsync(CancellationToken cancellationToken)
    {
        _server.ClearPools();
        await _server.ExecuteAsync(BringBackSql(_server.Provider, _name), NoParameters, cancellationToken);
        _away = false;
    }

    /// <summary>
    /// Makes every write to the database fail, as on a failover's secondary or a database read-only on purpose:
    /// SQL Server sets it READ_ONLY (3906), PostgreSQL makes every new transaction read-only (25006). Open sessions are
    /// ended, so none keeps writing.
    /// </summary>
    public async Task SetReadOnlyAsync(bool readOnly, CancellationToken cancellationToken)
    {
        _server.ClearPools();
        await _server.ExecuteAsync(ReadOnlySql(_server.Provider, _name, readOnly), NoParameters, cancellationToken);
        _readOnly = readOnly;
        await UntilItAcceptsConnectionsAsync(cancellationToken);
    }

    // SQL Server refuses logins for a moment while it changes a database's state, so a test that reads right after
    // would fail on the change rather than on what it checks.
    private async Task UntilItAcceptsConnectionsAsync(CancellationToken cancellationToken)
    {
        var tries = 0;
        while (true)
        {
            try
            {
                _database.ClearPools();
                await _database.ExecuteAsync("SELECT 1", NoParameters, cancellationToken);
                return;
            }
            catch (DbException) when (++tries < 50)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(200), cancellationToken);
            }
        }
    }

    private static string ReadOnlySql(TestDatabaseProvider provider, string name, bool readOnly) => provider switch
    {
        TestDatabaseProvider.PostgreSql => $"""
            ALTER DATABASE {name} SET default_transaction_read_only = {(readOnly ? "on" : "off")};
            SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{name}' AND pid <> pg_backend_pid();
            """,
        TestDatabaseProvider.SqlServer => $"ALTER DATABASE [{name}] SET {(readOnly ? "READ_ONLY" : "READ_WRITE")} WITH ROLLBACK IMMEDIATE",
        _ => throw ConnectionTarget.Unsupported(provider),
    };

    private static string TakeAwaySql(TestDatabaseProvider provider, string name) => provider switch
    {
        TestDatabaseProvider.PostgreSql => $"""
            SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname = '{name}' AND pid <> pg_backend_pid();
            ALTER DATABASE {name} RENAME TO {name}_away;
            """,
        TestDatabaseProvider.SqlServer => $"ALTER DATABASE [{name}] SET OFFLINE WITH ROLLBACK IMMEDIATE",
        _ => throw ConnectionTarget.Unsupported(provider),
    };

    private static string BringBackSql(TestDatabaseProvider provider, string name) => provider switch
    {
        TestDatabaseProvider.PostgreSql => $"ALTER DATABASE {name}_away RENAME TO {name}",
        TestDatabaseProvider.SqlServer => $"ALTER DATABASE [{name}] SET ONLINE",
        _ => throw ConnectionTarget.Unsupported(provider),
    };

    public async ValueTask DisposeAsync()
    {
        // A test that failed while its database was away still drops it: neither provider drops it from there.
        if (_away)
        {
            await BringBackAsync(CancellationToken.None);
        }

        if (_readOnly)
        {
            await SetReadOnlyAsync(false, CancellationToken.None);
        }

        // Pooled connections hold the database open, and both providers refuse to drop a database in use.
        _server.ClearPools();
        await _server.ExecuteAsync(DropDatabaseSql(_server.Provider, _name), NoParameters, CancellationToken.None);
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
                    new Npgsql.NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ConnectionString,
                TestDatabaseProvider.SqlServer =>
                    new SqlConnectionStringBuilder(ConnectionString) { InitialCatalog = name }.ConnectionString,
                _ => throw Unsupported(Provider),
            },
        };

        public void ClearPools()
        {
            switch (Provider)
            {
                case TestDatabaseProvider.PostgreSql:
                    Npgsql.NpgsqlConnection.ClearAllPools();
                    break;
                case TestDatabaseProvider.SqlServer:
                    SqlConnection.ClearAllPools();
                    break;
                default:
                    throw Unsupported(Provider);
            }
        }

        public async Task ExecuteAsync(
            string sql,
            IReadOnlyDictionary<string, object> parameters,
            CancellationToken cancellationToken)
        {
            await using var connection = await OpenAsync(cancellationToken);
            await using var command = CommandFor(connection, sql);
            foreach (var (name, value) in parameters)
            {
                command.Parameters.Add(ParameterFor(command, name, value));
            }

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public async Task<DbConnection> OpenAsync(CancellationToken cancellationToken)
        {
            DbConnection connection = Provider switch
            {
                TestDatabaseProvider.PostgreSql => new Npgsql.NpgsqlConnection(ConnectionString),
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

        // SQL Server gets a time as datetime2, because datetime, its default for a DateTime, rounds to about 3 ms.
        // PostgreSQL keeps its default: a UTC DateTime as timestamptz, which datetime2 would turn into a timestamp.
        private DbParameter ParameterFor(DbCommand command, string name, object value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            if (value is DateTime && Provider == TestDatabaseProvider.SqlServer)
            {
                parameter.DbType = DbType.DateTime2;
            }

            return parameter;
        }

        public static DbCommand CommandFor(DbConnection connection, string sql)
        {
            var command = connection.CreateCommand();
            command.CommandText = sql;
            return command;
        }

        public static ArgumentOutOfRangeException Unsupported(TestDatabaseProvider provider) =>
            new(nameof(provider), provider, "Unsupported test database provider.");

        // A record prints every member, the connection string and its password with it, wherever it is shown: in an
        // assertion's message, a debugger, a log.
        public override string ToString() => $"{Provider} connection";
    }
}
