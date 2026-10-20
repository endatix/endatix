using System.Runtime.CompilerServices;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Persistence.SqlServer.Repositories;

public sealed class SubmissionExportRepository : ISubmissionExportRepository
{
    private const int DefaultExportPageSize = 500;

    private readonly AppDbContext _dbContext;

    public SubmissionExportRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async IAsyncEnumerable<T> GetExportRowsAsync<T>(long formId, string? sqlFunctionName, int? pageSize, bool includeTestSubmissions, [EnumeratorCancellation] CancellationToken cancellationToken) where T : class, IExportItem
    {
        var procedureName = sqlFunctionName ?? "export_form_submissions";
        var effectivePageSize = pageSize ?? DefaultExportPageSize;
        var usePaging = effectivePageSize != 0 && typeof(T) == typeof(SubmissionExportRow);
        var stream = usePaging
            ? StreamPagedAsync<T>(formId, procedureName, effectivePageSize, includeTestSubmissions, cancellationToken)
            : StreamAllAsync<T>(formId, procedureName, includeTestSubmissions, cancellationToken);

        await foreach (var row in stream)
        {
            yield return row;
        }
    }

    private async IAsyncEnumerable<T> StreamAllAsync<T>(long formId, string procedureName, bool includeTestSubmissions, [EnumeratorCancellation] CancellationToken cancellationToken) where T : class, IExportItem
    {
        // Custom procedures do not take the flag; the endpoint rejects it for them.
        var sql = ExecProcedure(procedureName, "@form_id" + IncludeTestSubmissionsArgument(includeTestSubmissions));
        await foreach (var row in _dbContext.Set<T>()
            .FromSqlRaw(sql, new SqlParameter("@form_id", formId))
            .AsNoTracking()
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            yield return row;
        }
    }

    private async IAsyncEnumerable<T> StreamPagedAsync<T>(long formId, string procedureName, int pageSize, bool includeTestSubmissions, [EnumeratorCancellation] CancellationToken cancellationToken) where T : class, IExportItem
    {
        long? afterId = null;
        var sql = ExecProcedure(procedureName, "@form_id, @after_id, @page_size" + IncludeTestSubmissionsArgument(includeTestSubmissions));

        while (true)
        {
            var batch = await _dbContext.Set<T>()
                .FromSqlRaw(sql,
                    new SqlParameter("@form_id", formId),
                    new SqlParameter("@after_id", (object?)afterId ?? DBNull.Value),
                    new SqlParameter("@page_size", pageSize))
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            foreach (var row in batch)
            {
                yield return row;
            }

            if (batch.Count < pageSize)
            {
                break;
            }

            if (batch[^1] is not SubmissionExportRow last)
            {
                break;
            }

            afterId = last.Id;
        }
    }

    private static string ExecProcedure(string procedureName, string parameters)
    {
        var name = SqlIdentifier.EnsureValid(procedureName);

        // A schema-qualified name keeps its schema; "dbo.reporting.x" would read as database.schema.name.
        var qualifiedName = name.Contains('.') ? name : $"dbo.{name}";
        return $"EXEC {qualifiedName} {parameters}";
    }

    private static string IncludeTestSubmissionsArgument(bool includeTestSubmissions) =>
        includeTestSubmissions ? ", @include_test_submissions = 1" : string.Empty;
}
