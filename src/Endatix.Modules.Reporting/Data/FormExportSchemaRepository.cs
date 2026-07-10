using Endatix.Modules.Reporting.Domain;
using Endatix.Modules.Reporting.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Reporting.Data;

/// <summary>
/// Repository for form export schemas.
/// </summary>
internal sealed class FormExportSchemaRepository(
    ReportingDbContext dbContext,
    IReportingUnitOfWork unitOfWork) : IFormExportSchemaRepository
{
    /// <inheritdoc />
    public async Task<FormExportSchema?> GetByFormIdAsync(
        long tenantId,
        long formId,
        CancellationToken cancellationToken)
    {
        return await dbContext.FormExportSchemas
            .Where(schema => schema.TenantId == tenantId && schema.FormId == formId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task SaveAsync(FormExportSchema schema, CancellationToken cancellationToken)
    {
        if (schema.Id == default)
        {
            await dbContext.FormExportSchemas.AddAsync(schema, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
