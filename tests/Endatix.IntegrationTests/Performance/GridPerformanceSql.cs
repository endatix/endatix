using System.Data.Common;
using System.Text;
using System.Text.RegularExpressions;
using Endatix.IntegrationTests.Shared;

namespace Endatix.IntegrationTests;

/// <summary>Raw SQL the performance run needs around the measured queries: statistics, storage and plans.</summary>
internal static partial class GridPerformanceSql
{
    public static async Task ExecuteAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 1800;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task<List<object?[]>> QueryAsync(DbConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = 1800;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        List<object?[]> rows = [];
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new object?[reader.FieldCount];
            for (var column = 0; column < row.Length; column++)
            {
                row[column] = reader.IsDBNull(column) ? null : reader.GetValue(column);
            }

            rows.Add(row);
        }

        return rows;
    }

    /// <summary>
    /// Writes the core submission of every loaded row, created when the row was prepared, so the grid's join finds
    /// each row current.
    /// </summary>
    public static async Task LoadCoreSubmissionsAsync(
        DbConnection connection,
        TestDatabaseProvider provider,
        RebuildForm form,
        CancellationToken cancellationToken)
    {
        var definitionId = form.DefinitionIds[0];
        if (provider == TestDatabaseProvider.SqlServer)
        {
            await ExecuteAsync(connection, $$"""
                INSERT INTO dbo.Submissions (Id, TenantId, FormId, FormDefinitionId, IsComplete, IsDeleted, IsTestSubmission,
                  JsonData, CreatedAt, Revision, Status, CollectionStatus)
                SELECT SubmissionId, TenantId, FormId, {{definitionId}}, 1, 0, 0, CAST('{}' AS json), SourceModifiedAt, 1,
                  'new', 'complete'
                FROM reporting.FlattenedSubmissions WHERE FormId = {{form.FormId}};
                UPDATE STATISTICS dbo.Submissions WITH FULLSCAN;
                """, cancellationToken);
            return;
        }

        await ExecuteAsync(connection, $$"""
            INSERT INTO "Submissions" ("Id", "TenantId", "FormId", "FormDefinitionId", "IsComplete", "IsDeleted",
              "IsTestSubmission", "JsonData", "CreatedAt", "Revision", "Status", "CollectionStatus")
            SELECT "SubmissionId", "TenantId", "FormId", {{definitionId}}, true, false, false, '{}'::jsonb, "SourceModifiedAt", 1,
              'new', 'complete'
            FROM reporting."FlattenedSubmissions" WHERE "FormId" = {{form.FormId}};
            ANALYZE "Submissions";
            """, cancellationToken);
    }

    public static Task AnalyzeAsync(DbConnection connection, TestDatabaseProvider provider, CancellationToken cancellationToken) =>
        ExecuteAsync(
            connection,
            provider == TestDatabaseProvider.SqlServer
                ? "UPDATE STATISTICS reporting.FlattenedSubmissions WITH FULLSCAN;"
                : "VACUUM ANALYZE reporting.\"FlattenedSubmissions\";",
            cancellationToken);

    public static async Task<string> StorageAsync(DbConnection connection, TestDatabaseProvider provider, CancellationToken cancellationToken)
    {
        if (provider == TestDatabaseProvider.SqlServer)
        {
            var sizes = (await QueryAsync(connection, """
                SELECT AVG(CAST(DATALENGTH(DataJson) AS bigint)), AVG(CAST(DATALENGTH(SearchJson) AS bigint)),
                  (SELECT SUM(a.total_pages) * 8 / 1024 FROM sys.partitions p JOIN sys.allocation_units a ON a.container_id = p.partition_id
                   WHERE p.object_id = OBJECT_ID('reporting.FlattenedSubmissions'))
                FROM reporting.FlattenedSubmissions
                """, cancellationToken)).Single();
            return $"DataJson {sizes[0]} B, SearchJson {sizes[1]} B per row; table {sizes[2]} MB";
        }

        var stored = (await QueryAsync(connection, """
            SELECT avg(octet_length("DataJson"::text))::int, avg(pg_column_size("DataJson"))::int, avg(pg_column_size("SearchJson"))::int,
              count(*) FILTER (WHERE pg_column_size("SearchJson") > 2000), pg_size_pretty(pg_total_relation_size('reporting."FlattenedSubmissions"'))
            FROM reporting."FlattenedSubmissions"
            """, cancellationToken)).Single();
        return $"DataJson {stored[0]} B as text, {stored[1]} B stored; SearchJson {stored[2]} B stored, {stored[3]} rows over 2 KB (TOASTed); table {stored[4]}";
    }

    /// <summary>The PostgreSQL plan of a query, with its parameters written in, as <c>EXPLAIN (ANALYZE, BUFFERS)</c> prints it.</summary>
    public static async Task<string> ExplainAsync(DbConnection connection, string queryString, CancellationToken cancellationToken)
    {
        var plan = await QueryAsync(connection, "EXPLAIN (ANALYZE, BUFFERS) " + InlineParameters(queryString), cancellationToken);
        return string.Join('\n', plan.Select(row => row[0]));
    }

    // ToQueryString lists each parameter as a comment line (-- @p='value'); EXPLAIN needs them in the statement.
    private static string InlineParameters(string queryString)
    {
        Dictionary<string, string> values = [];
        StringBuilder sql = new();
        foreach (var line in queryString.Split('\n'))
        {
            var parameter = ParameterLine().Match(line);
            if (parameter.Success)
            {
                values[parameter.Groups["name"].Value] = parameter.Groups["value"].Value;
            }
            else if (!line.StartsWith("--", StringComparison.Ordinal))
            {
                sql.AppendLine(line);
            }
        }

        return values.OrderByDescending(value => value.Key.Length).Aggregate(
            sql.ToString(),
            (text, value) => text.Replace(value.Key, "'" + value.Value.Replace("'", "''", StringComparison.Ordinal) + "'", StringComparison.Ordinal));
    }

    [GeneratedRegex(@"^-- (?<name>@\w+)='(?<value>.*?)'( \(.*\))?\s*$")]
    private static partial Regex ParameterLine();
}
