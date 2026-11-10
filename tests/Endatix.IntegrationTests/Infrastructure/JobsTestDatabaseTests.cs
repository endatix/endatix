using System.Data.Common;
using Endatix.IntegrationTests.Infrastructure.Jobs;
using Endatix.IntegrationTests.Shared;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace Endatix.IntegrationTests;

/// <summary>
/// A test's own database is dropped when it is disposed, even when the state the test left it in cannot be undone; the
/// failure the test sees is the one that kept it from being undone.
/// </summary>
[Collection(nameof(DbIntegrationTestCollection))]
[Trait("Category", "Infrastructure")]
[Trait("Priority", "P1")]
public sealed class JobsTestDatabaseTests(DbIntegrationFixture fixture)
{
    [PostgreSqlSpecificFact]
    public async Task A_database_that_cannot_be_brought_back_is_still_dropped_and_its_failure_thrown()
    {
        // Arrange — PostgreSQL-only: the database is renamed away and another takes its name, so renaming it back fails.
        var ct = TestContext.Current.CancellationToken;
        var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        var name = new NpgsqlConnectionStringBuilder(database.ConnectionString).Database!;
        try
        {
            await database.TakeAwayAsync(ct);
            await ExecuteAsync($"CREATE DATABASE {name}", ct);

            // Act
            var thrown = await Record.ExceptionAsync(() => database.DisposeAsync().AsTask());
            var left = await CountAsync($"SELECT count(*) FROM pg_database WHERE datname = '{name}_away'", ct);

            // Assert
            Assert.Equal(PostgresErrorCodes.DuplicateDatabase, Assert.IsType<PostgresException>(thrown).SqlState);
            Assert.Equal(0, left);
        }
        finally
        {
            await DropPostgreSqlLeftoversAsync(name);
        }
    }

    [PostgreSqlSpecificFact]
    public async Task A_database_that_cannot_be_made_writable_again_is_still_dropped_and_its_failure_thrown()
    {
        // Arrange — PostgreSQL-only: the read-only database then refuses connections, so the wait for it to accept them
        // once it is writable again runs out.
        var ct = TestContext.Current.CancellationToken;
        var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        var name = new NpgsqlConnectionStringBuilder(database.ConnectionString).Database!;
        try
        {
            await database.SetReadOnlyAsync(true, ct);
            await ExecuteAsync($"ALTER DATABASE {name} ALLOW_CONNECTIONS false", ct);

            // Act
            var thrown = await Record.ExceptionAsync(() => database.DisposeAsync().AsTask());
            var left = await CountAsync($"SELECT count(*) FROM pg_database WHERE datname = '{name}'", ct);

            // Assert
            Assert.Equal(PostgresErrorCodes.ObjectNotInPrerequisiteState, Assert.IsType<PostgresException>(thrown).SqlState);
            Assert.Equal(0, left);
        }
        finally
        {
            await DropPostgreSqlLeftoversAsync(name);
        }
    }

    [Fact]
    [Trait("DbSpecific", "SqlServer")]
    public async Task A_database_that_cannot_be_brought_back_online_is_still_dropped_and_its_failure_thrown()
    {
        // Arrange — SQL Server-only: the offline database's data file is pointed at a file that does not exist, so
        // bringing it back online fails and leaves it recovery-pending.
        Assert.SkipWhen(fixture.Provider != TestDatabaseProvider.SqlServer, "Moves a database's data file in T-SQL.");
        var ct = TestContext.Current.CancellationToken;
        var database = await JobsTestDatabase.CreateAsync(fixture, ct);
        var name = new SqlConnectionStringBuilder(database.ConnectionString).InitialCatalog;
        DatabaseFiles? files = null;
        try
        {
            files = await DatabaseFilesAsync(name, ct);
            await database.TakeAwayAsync(ct);
            await ExecuteAsync(
                $"ALTER DATABASE [{name}] MODIFY FILE (NAME = N'{files.DataName}', FILENAME = N'{files.Data}.missing')", ct);

            // Act
            var thrown = await Record.ExceptionAsync(() => database.DisposeAsync().AsTask());
            var left = await CountAsync($"SELECT count(*) FROM sys.databases WHERE name = N'{name}'", ct);

            // Assert — 5120: SQL Server cannot open the data file.
            Assert.Equal(5120, Assert.IsType<SqlException>(thrown).Number);
            Assert.Equal(0, left);
        }
        finally
        {
            await DropSqlServerLeftoversAsync(name, files);
        }
    }

    private sealed record DatabaseFiles(string DataName, string Data, string Log);

    private async Task<DatabaseFiles> DatabaseFilesAsync(string name, CancellationToken ct)
    {
        await using var connection = await OpenServerAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            SELECT
                (SELECT name FROM sys.master_files WHERE database_id = DB_ID(N'{name}') AND type = 0),
                (SELECT physical_name FROM sys.master_files WHERE database_id = DB_ID(N'{name}') AND type = 0),
                (SELECT physical_name FROM sys.master_files WHERE database_id = DB_ID(N'{name}') AND type = 1)
            """;
        await using var reader = await command.ExecuteReaderAsync(ct);
        await reader.ReadAsync(ct);
        return new DatabaseFiles(reader.GetString(0), reader.GetString(1), reader.GetString(2));
    }

    // Two commands, since PostgreSQL refuses DROP DATABASE inside the transaction a multi-statement command runs in.
    private async Task DropPostgreSqlLeftoversAsync(string name)
    {
        await ExecuteAsync($"DROP DATABASE IF EXISTS {name}_away WITH (FORCE)", CancellationToken.None);
        await ExecuteAsync($"DROP DATABASE IF EXISTS {name} WITH (FORCE)", CancellationToken.None);
    }

    // SQL Server keeps the files of a database dropped while it was not online, so files still there are attached
    // again and dropped with the database, which deletes them.
    private async Task DropSqlServerLeftoversAsync(string name, DatabaseFiles? files)
    {
        await ExecuteAsync($"IF DB_ID(N'{name}') IS NOT NULL DROP DATABASE [{name}]", CancellationToken.None);
        if (files is not null)
        {
            await ExecuteAsync($"""
                IF EXISTS (SELECT 1 FROM sys.dm_os_file_exists(N'{files.Data}') WHERE file_exists = 1)
                BEGIN
                    CREATE DATABASE [{name}] ON (FILENAME = N'{files.Data}'), (FILENAME = N'{files.Log}') FOR ATTACH;
                    DROP DATABASE [{name}];
                END
                """, CancellationToken.None);
        }
    }

    private async Task ExecuteAsync(string sql, CancellationToken ct)
    {
        await using var connection = await OpenServerAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(ct);
    }

    private async Task<long> CountAsync(string sql, CancellationToken ct)
    {
        await using var connection = await OpenServerAsync(ct);
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(ct));
    }

    private async Task<DbConnection> OpenServerAsync(CancellationToken ct)
    {
        DbConnection connection = fixture.Provider == TestDatabaseProvider.SqlServer
            ? new SqlConnection(fixture.ServerConnectionString)
            : new NpgsqlConnection(fixture.ServerConnectionString);
        try
        {
            await connection.OpenAsync(ct);
            return connection;
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }
}
