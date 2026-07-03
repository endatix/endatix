using System.Data.Common;
using Microsoft.Data.SqlClient;
using Npgsql;

namespace Endatix.IntegrationTests.Shared;

/// <summary>
/// Provider-neutral SQL assertions for integration tests (schema objects, ad-hoc existence checks).
/// </summary>
public static class IntegrationDbAssert
{
    /// <summary>
    /// Checks if a table exists in the database.
    /// </summary>
    /// <param name="connectionString">The connection string to the database.</param>
    /// <param name="provider">The database provider.</param>
    /// <param name="schema">The schema of the table.</param>
    /// <param name="table">The name of the table.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the table exists, false otherwise.</returns>
    public static async Task<bool> TableExistsAsync(
        string connectionString,
        TestDatabaseProvider provider,
        string schema,
        string table,
        CancellationToken cancellationToken = default)
    {
        var sql = provider switch
        {
            TestDatabaseProvider.PostgreSql =>
                "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = @schema AND table_name = @table)",
            TestDatabaseProvider.SqlServer =>
                "SELECT CASE WHEN EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @schema AND TABLE_NAME = @table) THEN 1 ELSE 0 END",
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported test database provider.")
        };

        return await ExecuteParameterizedExistsQueryAsync(
            connectionString,
            provider,
            sql,
            cancellationToken,
            ("@schema", schema),
            ("@table", table));
    }

    /// <summary>
    /// Checks if a routine exists in the database.
    /// </summary>
    /// <param name="connectionString">The connection string to the database.</param>
    /// <param name="provider">The database provider.</param>
    /// <param name="schema">The schema of the routine.</param>
    /// <param name="routineName">The name of the routine.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the routine exists, false otherwise.</returns>
    public static async Task<bool> RoutineExistsAsync(
        string connectionString,
        TestDatabaseProvider provider,
        string schema,
        string routineName,
        CancellationToken cancellationToken = default)
    {
        var sql = provider switch
        {
            TestDatabaseProvider.PostgreSql =>
                """
                SELECT EXISTS (
                    SELECT 1
                    FROM information_schema.routines
                    WHERE routine_schema = @schema
                      AND routine_name = @routine)
                """,
            TestDatabaseProvider.SqlServer =>
                """
                SELECT CASE WHEN EXISTS (
                    SELECT 1
                    FROM INFORMATION_SCHEMA.ROUTINES
                    WHERE ROUTINE_SCHEMA = @schema
                      AND ROUTINE_NAME = @routine)
                THEN 1 ELSE 0 END
                """,
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported test database provider.")
        };

        return await ExecuteParameterizedExistsQueryAsync(
            connectionString,
            provider,
            sql,
            cancellationToken,
            ("@schema", schema),
            ("@routine", routineName));
    }

    /// <summary>
    /// Checks if a row exists in the database.
    /// </summary>
    /// <param name="connectionString">The connection string to the database.</param>
    /// <param name="provider">The database provider.</param>
    /// <param name="sql">The SQL query to execute.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>True if the row exists, false otherwise.</returns>
    public static async Task<bool> SqlRowExistsAsync(
        string connectionString,
        TestDatabaseProvider provider,
        string sql,
        CancellationToken cancellationToken = default)
    {
        await using var connection = OpenConnection(connectionString, provider);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is bool boolResult ? boolResult : Convert.ToInt32(result) == 1;
    }

    private static async Task<bool> ExecuteParameterizedExistsQueryAsync(
        string connectionString,
        TestDatabaseProvider provider,
        string sql,
        CancellationToken cancellationToken,
        params (string Name, string Value)[] parameters)
    {
        await using var connection = OpenConnection(connectionString, provider);
        await connection.OpenAsync(cancellationToken);

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        foreach ((var name, var value) in parameters)
        {
            var param = command.CreateParameter();
            param.ParameterName = name;
            param.Value = value;
            command.Parameters.Add(param);
        }

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is bool boolResult ? boolResult : Convert.ToInt32(result) == 1;
    }

    private static DbConnection OpenConnection(string connectionString, TestDatabaseProvider provider) =>
        provider switch
        {
            TestDatabaseProvider.PostgreSql => new NpgsqlConnection(connectionString),
            TestDatabaseProvider.SqlServer => new SqlConnection(connectionString),
            _ => throw new ArgumentOutOfRangeException(nameof(provider), provider, "Unsupported test database provider.")
        };
}
