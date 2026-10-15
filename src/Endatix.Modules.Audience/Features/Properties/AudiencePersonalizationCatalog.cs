using Endatix.Core.Abstractions.Submissions;
using Endatix.Infrastructure.Data;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Properties;

/// <summary>
/// Reads a form's properties for an export: one indexed read per export, so a rename or reorder shows on the next
/// export without touching the stored form schema.
/// </summary>
internal sealed class AudiencePersonalizationCatalog(IAudienceDbContext db) : IPersonalizationCatalog
{
    public async Task<IReadOnlyList<PersonalizationVariableInfo>> ListForFormAsync(
        long tenantId,
        long formId,
        CancellationToken cancellationToken) =>
        (await ReadRowsAsync(tenantId, formId, cancellationToken))
            .GroupBy(property => property.VariableName, StringComparer.Ordinal)
            .Select(Current)
            .ToList();

    // Deleted properties are read too: a response may have frozen their values, so their columns stay.
    private Task<List<PropertyRow>> ReadRowsAsync(long tenantId, long formId, CancellationToken cancellationToken) =>
        db.Properties
            .AsNoTracking()
            .IgnoreQueryFilters([EndatixQueryFilterNames.SoftDelete])
            .Where(property => property.TenantId == tenantId && property.FormId == formId)
            .Select(property => new PropertyRow(
                property.VariableName,
                property.Name,
                property.SortOrder,
                property.IsDeleted,
                property.DeletedAt))
            .ToListAsync(cancellationToken);

    private sealed record PropertyRow(string VariableName, string Name, int SortOrder, bool IsDeleted, DateTime? DeletedAt);

    // A variable name is unique among live properties, so a group has at most one live row.
    private static PersonalizationVariableInfo Current(IEnumerable<PropertyRow> rows)
    {
        var current = rows
            .OrderBy(row => row.IsDeleted)
            .ThenByDescending(row => row.DeletedAt)
            .First();
        return new PersonalizationVariableInfo(current.VariableName, current.Name, current.SortOrder, current.IsDeleted);
    }
}
