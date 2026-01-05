using Endatix.Core.Abstractions.Exporting;
using Endatix.Core.Abstractions.Repositories;
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

    public IAsyncEnumerable<T> GetExportRowsAsync<T>(long formId, string? sqlFunctionName, CancellationToken cancellationToken) where T : class, IExportItem
    {
        var procedureName = sqlFunctionName ?? "export_form_submissions";
        var sql = "EXEC dbo." + procedureName + " @form_id";

        return _dbContext.Set<T>()
            .FromSqlRaw(sql, new SqlParameter("@form_id", formId))
            .AsNoTracking()
            .AsAsyncEnumerable();
    }
}