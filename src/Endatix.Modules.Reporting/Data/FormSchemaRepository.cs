using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Reporting.Data;

/// <summary>
/// Repository for compiled form schemas.
/// </summary>
internal sealed class FormSchemaRepository(
    ReportingDbContext dbContext,
    IReportingUnitOfWork unitOfWork) : IFormSchemaRepository
{
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
    public async Task SaveAsync(FormSchema schema, CancellationToken cancellationToken)
    {
        if (schema.Id == default)
        {
            await dbContext.FormSchemas.AddAsync(schema, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
