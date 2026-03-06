using System.Runtime.CompilerServices;
using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Endatix.Persistence.PostgreSql.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Endatix.Persistence.PostgreSql.Repositories;

public sealed class SubmissionExportRepository : ISubmissionExportRepository
{
    private const int DefaultExportCommandTimeoutSeconds = 300;
    private const int DefaultExportPageSize = 500;

    private readonly AppDbContext _dbContext;
    private readonly PostgreSqlOptions _options;

    public SubmissionExportRepository(AppDbContext dbContext, IOptions<PostgreSqlOptions> options)
    {
        _dbContext = dbContext;
        _options = options.Value;
    }

    public async IAsyncEnumerable<T> GetExportRowsAsync<T>(long formId, string? sqlFunctionName, int? pageSize, [EnumeratorCancellation] CancellationToken cancellationToken) where T : class, IExportItem
    {
        var timeoutSeconds = _options.ExportCommandTimeout ?? DefaultExportCommandTimeoutSeconds;
        _dbContext.Database.SetCommandTimeout(TimeSpan.FromSeconds(timeoutSeconds));

        var functionName = sqlFunctionName ?? "export_form_submissions";
        var effectivePageSize = pageSize ?? _options.ExportPageSize ?? DefaultExportPageSize;
        var usePaging = effectivePageSize != 0 && typeof(T) == typeof(SubmissionExportRow);
        var rowsStream = usePaging
            ? StreamPagedAsync<T>(formId, functionName, effectivePageSize, cancellationToken)
            : StreamAllAsync<T>(formId, functionName, cancellationToken);

        await foreach (var row in rowsStream)
        {
            yield return row;
        }
    }

    private async IAsyncEnumerable<T> StreamAllAsync<T>(long formId, string functionName, [EnumeratorCancellation] CancellationToken cancellationToken) where T : class, IExportItem
    {
        var sql = $@"SELECT * FROM {functionName}({{0}})";
        await foreach (var row in _dbContext.Set<T>()
            .FromSqlRaw(sql, formId)
            .AsNoTracking()
            .AsAsyncEnumerable()
            .WithCancellation(cancellationToken))
        {
            yield return row;
        }
    }

    private async IAsyncEnumerable<T> StreamPagedAsync<T>(long formId, string functionName, int pageSize, [EnumeratorCancellation] CancellationToken cancellationToken) where T : class, IExportItem
    {
        long? afterId = null;
        var sql = $@"SELECT * FROM {functionName}({{0}}, {{1}}, {{2}})";

        while (true)
        {
            var batch = await _dbContext.Set<T>()
                .FromSqlRaw(sql, formId, afterId ?? (object)DBNull.Value, pageSize)
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
}
