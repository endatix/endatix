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

    public IAsyncEnumerable<SubmissionExportRow> GetExportRowsAsync(long formId, CancellationToken cancellationToken)
    {
        return _dbContext.Set<SubmissionExportRow>()
            .FromSqlRaw(@"
                SELECT
                    ""FormId"",
                    ""Id"",
                    ""IsComplete"",
                    ""CompletedAt"",
                    ""CreatedAt"",
                    ""ModifiedAt"",
                    ""FlattenedAnswers""::text AS ""AnswersModel""
                FROM export_form_submissions_nested_loops({0})", formId)
            .AsNoTracking()
            .AsAsyncEnumerable();
    }
}