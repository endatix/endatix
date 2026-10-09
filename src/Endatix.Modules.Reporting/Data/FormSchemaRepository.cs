using System.Globalization;
using Endatix.Infrastructure.Data.Locking;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Reporting.Data;

/// <summary>
/// Repository for compiled form schemas.
/// </summary>
/// <remarks>
/// Schemas are read untracked and tracked only while they are saved, so every read comes from the database: a
/// scope that lives long, as a backfill does, sees what other scopes committed since, and never what it saved itself
/// in a transaction that then rolled back.
/// </remarks>
internal sealed class FormSchemaRepository(
    ReportingDbContext dbContext,
    IReportingUnitOfWork unitOfWork,
    ITransactionLock transactionLock) : IFormSchemaRepository
{
    /// <summary>
    /// The longest wait for another rebuild of the form. Far longer than a rebuild holds the lock, and well inside the
    /// 30-second default command timeout, so a wait that runs out fails as a lock timeout, which is retried.
    /// </summary>
    internal static readonly TimeSpan RebuildLockTimeout = TimeSpan.FromSeconds(10);

    /// <inheritdoc />
    public Task<FormSchema?> GetByFormIdAsync(
        long tenantId,
        long formId,
        CancellationToken cancellationToken) =>
        dbContext.FormSchemas
            .AsNoTracking()
            .Where(schema => schema.TenantId == tenantId && schema.FormId == formId)
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<FormSchema?> LockAndGetByFormIdAsync(
        long tenantId,
        long formId,
        CancellationToken cancellationToken)
    {
        await transactionLock.AcquireAsync(dbContext.Database, RebuildLock(tenantId, formId), cancellationToken);
        return await GetByFormIdAsync(tenantId, formId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveAsync(FormSchema schema, CancellationToken cancellationToken)
    {
        var entry = schema.Id == default
            ? await dbContext.FormSchemas.AddAsync(schema, cancellationToken)
            : dbContext.FormSchemas.Update(schema);
        try
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        finally
        {
            entry.State = EntityState.Detached;
        }
    }

    /// <inheritdoc />
    public Task<int> DeleteByFormIdAsync(long tenantId, long formId, CancellationToken cancellationToken) =>
        dbContext.FormSchemas
            .IgnoreQueryFilters()
            .Where(schema => schema.TenantId == tenantId && schema.FormId == formId)
            .ExecuteDeleteAsync(cancellationToken);

    private static TransactionLockRequest RebuildLock(long tenantId, long formId) =>
        new(TransactionLockScopes.ReportingFormSchema, string.Create(CultureInfo.InvariantCulture, $"{tenantId}:{formId}"))
        {
            Timeout = RebuildLockTimeout,
        };
}
