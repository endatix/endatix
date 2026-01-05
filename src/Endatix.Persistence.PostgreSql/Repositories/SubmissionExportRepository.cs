using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Persistence.PostgreSql.Repositories;

public sealed class SubmissionExportRepository : ISubmissionExportRepository
{
    private readonly AppDbContext _dbContext;

    public SubmissionExportRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public IAsyncEnumerable<T> GetExportRowsAsync<T>(long formId, string? sqlFunctionName, CancellationToken cancellationToken) where T : class, IExportItem
    {
        var functionName = sqlFunctionName ?? "export_form_submissions";

        // For dynamic types we might need to handle the SQL differently if they don't share the same schema as SubmissionExportRow. For now, we assume the function name handles the return type mapping
        var sql = $@"SELECT * FROM {functionName}({{0}})";

        return _dbContext.Set<T>()
            .FromSqlRaw(sql, formId)
            .AsNoTracking()
            .AsAsyncEnumerable();
    }
}