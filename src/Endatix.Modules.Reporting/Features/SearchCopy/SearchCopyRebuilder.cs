using System.Data.Common;
using System.Text.Json;
using Ardalis.GuardClauses;
using Endatix.Modules.Reporting.Data;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;
using FlattenedSubmissionRow = Endatix.Modules.Reporting.Domain.FlattenedSubmission;

namespace Endatix.Modules.Reporting.Features.SearchCopy;

/// <summary>
/// Rebuilds the search copy of every processed row of a form from its stored answers, after the form's search keys
/// changed. Submissions are not flattened again, and the answers are not changed.
/// </summary>
/// <remarks>
/// Rows go in batches by submission id, one statement per batch, so no statement holds many rows' locks for long. Only
/// rows stamped with an older search keys revision than the schema's are rewritten (<see cref="SearchCopyRebuildSql"/>),
/// so running it again after an interrupted run finishes the rest. A flatten that read the schema's keys before they
/// changed and writes after its row's batch stamps the row with the old revision: filters count it as not checked
/// on the new keys until the next rebuild or flatten.
/// </remarks>
internal sealed class SearchCopyRebuilder(
    IReportingDbContext dbContext,
    IFormSchemaRepository schemaRepository,
    ILogger<SearchCopyRebuilder> logger)
{
    internal const int DefaultBatchSize = 1000;

    /// <summary>How many rows one statement rewrites.</summary>
    internal int BatchSize { get; init; } = DefaultBatchSize;

    /// <summary>Rebuilds the form's search copies; returns how many rows were rewritten.</summary>
    public async Task<int> RebuildFormAsync(long tenantId, long formId, CancellationToken cancellationToken)
    {
        Guard.Against.NegativeOrZero(tenantId);
        Guard.Against.NegativeOrZero(formId);

        var schema = await schemaRepository.GetByFormIdAsync(tenantId, formId, cancellationToken);
        if (schema is null)
        {
            return 0;
        }

        var searchKeys = FormSearchKeys.Of(schema);
        RebuildRun run = new(tenantId, formId, searchKeys.Revision, KeysParameter(searchKeys), Statement(searchKeys));
        var rows = await RunBatchesAsync(run, cancellationToken);
        logger.LogInformation("Rebuilt the search copy of {Rows} flattened row(s) of form {FormId}", rows, formId);
        return rows;
    }

    private async Task<int> RunBatchesAsync(RebuildRun run, CancellationToken cancellationToken)
    {
        await dbContext.Database.OpenConnectionAsync(cancellationToken);
        try
        {
            var rows = 0;
            long afterId = 0;
            while (await RunBatchAsync(run, afterId, cancellationToken) is { } batch)
            {
                rows += batch.Rows;
                afterId = batch.LastSubmissionId;
            }

            return rows;
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    private async Task<BatchResult?> RunBatchAsync(RebuildRun run, long afterId, CancellationToken cancellationToken)
    {
        await using var command = CreateCommand(run, afterId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || await reader.IsDBNullAsync(0, cancellationToken))
        {
            return null;
        }

        return new BatchResult(reader.GetInt64(0), Convert.ToInt32(reader.GetValue(1), System.Globalization.CultureInfo.InvariantCulture));
    }

    private DbCommand CreateCommand(RebuildRun run, long afterId)
    {
        var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = run.Sql;
        command.Transaction = dbContext.Database.CurrentTransaction?.GetDbTransaction();
        command.CommandTimeout = dbContext.Database.GetCommandTimeout() ?? command.CommandTimeout;
        AddParameter(command, "tenantId", run.TenantId);
        AddParameter(command, "formId", run.FormId);
        AddParameter(command, "revision", run.Revision);
        AddParameter(command, "afterId", afterId);
        AddParameter(command, "batchSize", BatchSize);
        AddParameter(command, "keys", run.KeysJson);
        return command;
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private string Statement(FormSearchKeys searchKeys)
    {
        var entityType = dbContext.FlattenedSubmissions.EntityType;
        var schema = entityType.GetSchema();
        var table = entityType.GetTableName() ?? nameof(FlattenedSubmissionRow);
        return dbContext.Database.IsNpgsql()
            ? SearchCopyRebuildSql.PostgreSql($"\"{schema}\".\"{table}\"")
            : SearchCopyRebuildSql.SqlServer($"[{schema}].[{table}]", searchKeys);
    }

    private static string KeysParameter(FormSearchKeys searchKeys) =>
        JsonSerializer.Serialize(searchKeys.Keys.ToDictionary(key => key.Key, key => key.FilterType, StringComparer.Ordinal));

    private sealed record RebuildRun(long TenantId, long FormId, int Revision, string KeysJson, string Sql);

    private sealed record BatchResult(long LastSubmissionId, int Rows);
}
