using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Data.Repositories;

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
            .FromSqlRaw("SELECT * FROM export_form_submissions({0})", formId)
            .AsNoTracking()
            .AsAsyncEnumerable();
    }
}