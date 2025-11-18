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

    public IAsyncEnumerable<SubmissionExportRow> GetExportRowsAsync(long formId, string? sqlFunctionName, CancellationToken cancellationToken)
    {
        var procedureName = sqlFunctionName ?? "export_form_submissions";

        return _dbContext.Set<SubmissionExportRow>()
            .FromSqlRaw($"EXEC dbo.{procedureName} @form_id",
                        new SqlParameter("@form_id", formId))
            .AsNoTracking()
            .AsAsyncEnumerable();
    }
}