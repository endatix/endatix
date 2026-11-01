using System.Data;
using System.Data.Common;
using Endatix.Modules.Reporting.Contracts;
using Endatix.Modules.Reporting.Features.SearchCopy;
using Microsoft.Data.SqlClient;
using Npgsql;
using NpgsqlTypes;

namespace Endatix.IntegrationTests;

/// <summary>
/// Bulk-loads processed flattened rows for the performance run: PostgreSQL binary <c>COPY</c>, SQL Server
/// <c>SqlBulkCopy</c> through a staging table (bulk copy cannot write the <c>json</c> type).
/// </summary>
internal sealed class GridPerformanceLoader(GridPerformanceShape shape, FormSearchKeys searchKeys, long formId)
{
    // Far above any snowflake id the test seeds, so loaded rows never collide with them.
    private const long FirstSubmissionId = 5_000_000_000_000L;
    private const int SqlServerChunk = 5000;
    private static readonly DateTime FirstCreatedAt = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public GridPerformanceCounts Counts { get; } = new();

    public Task LoadAsync(DbConnection connection, int rows, CancellationToken cancellationToken) =>
        connection is SqlConnection sqlServer
            ? LoadSqlServerAsync(sqlServer, rows, cancellationToken)
            : LoadPostgreSqlAsync((NpgsqlConnection)connection, rows, cancellationToken);

    private async Task LoadPostgreSqlAsync(NpgsqlConnection connection, int rows, CancellationToken cancellationToken)
    {
        await using var import = await connection.BeginBinaryImportAsync("""
            COPY reporting."FlattenedSubmissions" ("SubmissionId", "TenantId", "FormId", "DataJson", "SearchJson",
              "IsDeleted", "CreatedAt", "SourceModifiedAt", "SourceRevision", "IntegrationStatus",
              "Integration_LastAttemptAt", "Integration_ProcessedAt") FROM STDIN (FORMAT BINARY)
            """, cancellationToken);
        for (var index = 1; index <= rows; index++)
        {
            await WritePostgreSqlRowAsync(import, index, cancellationToken);
        }

        await import.CompleteAsync(cancellationToken);
    }

    private async Task WritePostgreSqlRowAsync(NpgsqlBinaryImporter import, int index, CancellationToken cancellationToken)
    {
        var row = shape.Row(index, searchKeys, Counts);
        var at = FirstCreatedAt.AddMinutes(index);
        await import.StartRowAsync(cancellationToken);
        await import.WriteAsync(FirstSubmissionId + index, NpgsqlDbType.Bigint, cancellationToken);
        await import.WriteAsync(FormSchemaRebuildWorld.TenantId, NpgsqlDbType.Bigint, cancellationToken);
        await import.WriteAsync(formId, NpgsqlDbType.Bigint, cancellationToken);
        await import.WriteAsync(row.DataJson, NpgsqlDbType.Jsonb, cancellationToken);
        await import.WriteAsync(row.SearchJson, NpgsqlDbType.Jsonb, cancellationToken);
        await import.WriteAsync(false, NpgsqlDbType.Boolean, cancellationToken);
        await import.WriteAsync(at, NpgsqlDbType.TimestampTz, cancellationToken);
        await import.WriteAsync(at, NpgsqlDbType.TimestampTz, cancellationToken);
        await import.WriteAsync(1L, NpgsqlDbType.Bigint, cancellationToken);
        await import.WriteAsync(SubmissionIntegrationStatusCodes.Processed, NpgsqlDbType.Varchar, cancellationToken);
        await import.WriteAsync(at, NpgsqlDbType.TimestampTz, cancellationToken);
        await import.WriteAsync(at, NpgsqlDbType.TimestampTz, cancellationToken);
    }

    private async Task LoadSqlServerAsync(SqlConnection connection, int rows, CancellationToken cancellationToken)
    {
        await GridPerformanceSql.ExecuteAsync(connection, """
            DROP TABLE IF EXISTS reporting.PerformanceStage;
            CREATE TABLE reporting.PerformanceStage (SubmissionId bigint NOT NULL, CreatedAt datetime2 NOT NULL,
              DataJson nvarchar(max) NOT NULL, SearchJson nvarchar(max) NOT NULL);
            """, cancellationToken);
        for (var first = 1; first <= rows; first += SqlServerChunk)
        {
            await StageSqlServerChunkAsync(connection, (first, Math.Min(first + SqlServerChunk - 1, rows)), cancellationToken);
            await GridPerformanceSql.ExecuteAsync(connection, $"""
                INSERT INTO reporting.FlattenedSubmissions (SubmissionId, TenantId, FormId, DataJson, SearchJson, IsDeleted,
                  CreatedAt, SourceModifiedAt, SourceRevision, IntegrationStatus, Integration_LastAttemptAt, Integration_ProcessedAt)
                SELECT SubmissionId, {FormSchemaRebuildWorld.TenantId}, {formId}, CAST(DataJson AS json), CAST(SearchJson AS json), 0,
                  CreatedAt, CreatedAt, 1, '{SubmissionIntegrationStatusCodes.Processed}', CreatedAt, CreatedAt
                FROM reporting.PerformanceStage;
                TRUNCATE TABLE reporting.PerformanceStage;
                """, cancellationToken);
        }

        await GridPerformanceSql.ExecuteAsync(connection, "DROP TABLE reporting.PerformanceStage", cancellationToken);
    }

    private async Task StageSqlServerChunkAsync(SqlConnection connection, (int First, int Last) chunk, CancellationToken cancellationToken)
    {
        using DataTable table = new();
        table.Columns.Add("SubmissionId", typeof(long));
        table.Columns.Add("CreatedAt", typeof(DateTime));
        table.Columns.Add("DataJson", typeof(string));
        table.Columns.Add("SearchJson", typeof(string));
        for (var index = chunk.First; index <= chunk.Last; index++)
        {
            var row = shape.Row(index, searchKeys, Counts);
            table.Rows.Add(FirstSubmissionId + index, FirstCreatedAt.AddMinutes(index), row.DataJson, row.SearchJson);
        }

        using SqlBulkCopy bulkCopy = new(connection) { DestinationTableName = "reporting.PerformanceStage", BulkCopyTimeout = 0 };
        await bulkCopy.WriteToServerAsync(table, cancellationToken);
    }
}
