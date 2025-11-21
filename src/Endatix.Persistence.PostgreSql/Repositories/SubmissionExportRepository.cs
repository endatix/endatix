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

    public IAsyncEnumerable<SubmissionExportRow> GetExportRowsAsync(long formId, string? sqlFunctionName, CancellationToken cancellationToken)
    {
        var functionName = sqlFunctionName ?? "export_form_submissions";

        var sql = $@"
            SELECT
                ""FormId"",
                ""Id"",
                ""IsComplete"",
                ""CompletedAt"",
                ""CreatedAt"",
                ""ModifiedAt"",
                ""AnswersModel""::text AS ""AnswersModel""
            FROM {functionName}({{0}})";

        return _dbContext.Set<SubmissionExportRow>()
            .FromSqlRaw(sql, formId)
            .AsNoTracking()
            .AsAsyncEnumerable();
    }
}