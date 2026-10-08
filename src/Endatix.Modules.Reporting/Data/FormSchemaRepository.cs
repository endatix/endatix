using System.Globalization;
using Endatix.Infrastructure.Data.Locking;
using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Reporting.Data;

/// <summary>
/// Repository for compiled form schemas.
/// </summary>
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
    public async Task<FormSchema?> GetByFormIdAsync(
        long tenantId,
        long formId,
        CancellationToken cancellationToken)
    {
        return await dbContext.FormSchemas
            .Where(schema => schema.TenantId == tenantId && schema.FormId == formId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<FormSchema?> LockAndGetByFormIdAsync(
        long tenantId,
        long formId,
        CancellationToken cancellationToken)
    {
        await transactionLock.AcquireAsync(dbContext.Database, RebuildLock(tenantId, formId), cancellationToken);

        // A row this context already tracks would come back as it was first read, before the lock, and hide what the
        // rebuild that held the lock saved; read it again instead.
        DetachTracked(tenantId, formId);
        return await GetByFormIdAsync(tenantId, formId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveAsync(FormSchema schema, CancellationToken cancellationToken)
    {
        if (schema.Id == default)
        {
            await dbContext.FormSchemas.AddAsync(schema, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
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

    private void DetachTracked(long tenantId, long formId)
    {
        var tracked = dbContext.FormSchemas.Local
            .FirstOrDefault(schema => schema.TenantId == tenantId && schema.FormId == formId);
        if (tracked is not null)
        {
            dbContext.Entry(tracked).State = EntityState.Detached;
        }
    }
}
