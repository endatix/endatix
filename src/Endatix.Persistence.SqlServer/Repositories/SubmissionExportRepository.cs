using Endatix.Core.Abstractions.Repositories;
using Endatix.Core.Entities;
using Endatix.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Persistence.SqlServer.Repositories;

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
            .FromSqlRaw("EXEC dbo.export_form_submissions @form_id",
                        new SqlParameter("@form_id", formId))
            .AsNoTracking()
            .AsAsyncEnumerable();
    }
}