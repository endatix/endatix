using Endatix.Modules.Audience.Contracts;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Properties;

/// <summary>
/// Reads a form's properties for an export: one read per export on <c>IX_Properties_Form</c>, so a rename or reorder
/// shows on the next export without touching the stored form schema.
/// </summary>
/// <remarks>
/// Live properties only, so the partial index serves the read. A deleted property's column keeps the name frozen in
/// the schema and sorts after the live ones.
/// </remarks>
internal sealed class AudiencePropertyCatalog(IAudienceDbContext db) : IAudiencePropertyCatalog
{
    public async Task<IReadOnlyList<AudiencePropertyName>> ListForFormAsync(
        long tenantId,
        long formId,
        CancellationToken cancellationToken) =>
        await db.Properties
            .AsNoTracking()
            .Where(property => property.TenantId == tenantId && property.FormId == formId)
            .InTabOrder()
            .Select(property => new AudiencePropertyName(property.VariableName, property.Name, property.SortOrder))
            .ToListAsync(cancellationToken);
}
