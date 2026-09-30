using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Personalization.Domain;
using Endatix.Modules.Personalization.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Personalization.Features.People;

/// <summary>
/// Shared write path for property value cells on a membership.
/// </summary>
internal static class AudiencePropertyValuesWriter
{
    public static async Task<Result> ValidatePropertyIdsAsync(
        IPersonalizationDbContext db,
        long formId,
        IEnumerable<long>? propertyIds,
        CancellationToken cancellationToken)
    {
        if (propertyIds is null)
        {
            return Result.Success();
        }

        List<long> ids = propertyIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return Result.Success();
        }

        int known = await db.AudienceProperties
            .CountAsync(
                property => property.FormId == formId && ids.Contains(property.Id),
                cancellationToken);

        return known == ids.Count
            ? Result.Success()
            : Result.Invalid(new ValidationError(
                "One or more property ids do not belong to this form."));
    }

    public static async Task UpsertAsync(
        IPersonalizationDbContext db,
        long tenantId,
        long membershipId,
        IReadOnlyDictionary<long, string> values,
        CancellationToken cancellationToken)
    {
        if (values.Count == 0)
        {
            return;
        }

        HashSet<long> propertyIds = values.Keys.ToHashSet();
        Dictionary<long, AudiencePropertyValue> existing = await db.AudiencePropertyValues
            .Where(value => value.AudienceMembershipId == membershipId
                && propertyIds.Contains(value.AudiencePropertyId))
            .ToDictionaryAsync(value => value.AudiencePropertyId, cancellationToken);

        foreach ((long propertyId, string value) in values)
        {
            if (existing.TryGetValue(propertyId, out AudiencePropertyValue? cell))
            {
                cell.SetValue(value);
            }
            else
            {
                db.AudiencePropertyValues.Add(
                    new AudiencePropertyValue(tenantId, membershipId, propertyId, value));
            }
        }
    }
}
