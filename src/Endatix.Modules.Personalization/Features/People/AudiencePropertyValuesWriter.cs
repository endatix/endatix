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
        PropertyIdCheck check,
        CancellationToken cancellationToken)
    {
        if (check.PropertyIds.Count == 0)
        {
            return Result.Success();
        }

        List<long> ids = check.PropertyIds.Distinct().ToList();
        int known = await check.Db.AudienceProperties.CountAsync(
            property => property.FormId == check.FormId && ids.Contains(property.Id),
            cancellationToken);

        return known == ids.Count
            ? Result.Success()
            : Result.Invalid(new ValidationError(
                "One or more property ids do not belong to this form."));
    }

    public static async Task UpsertAsync(
        AudienceValueWrite write,
        CancellationToken cancellationToken)
    {
        if (write.Values.Count == 0)
        {
            return;
        }

        HashSet<long> propertyIds = write.Values.Keys.ToHashSet();
        Dictionary<long, AudiencePropertyValue> existing = await write.Db.AudiencePropertyValues
            .Where(value => value.AudienceMembershipId == write.MembershipId
                && propertyIds.Contains(value.AudiencePropertyId))
            .ToDictionaryAsync(value => value.AudiencePropertyId, cancellationToken);

        foreach ((long propertyId, string value) in write.Values)
        {
            ApplyCell(write, existing, new KeyValuePair<long, string>(propertyId, value));
        }
    }

    private static void ApplyCell(
        AudienceValueWrite write,
        Dictionary<long, AudiencePropertyValue> existing,
        KeyValuePair<long, string> cellValue)
    {
        if (existing.TryGetValue(cellValue.Key, out AudiencePropertyValue? cell))
        {
            cell.SetValue(cellValue.Value);
            return;
        }

        write.Db.AudiencePropertyValues.Add(
            new AudiencePropertyValue(new AudiencePropertyValueCreateArgs(
                write.TenantId, write.MembershipId, cellValue.Key, cellValue.Value)));
    }
}

/// <summary>
/// Inputs for validating property ids against a form.
/// </summary>
internal sealed record PropertyIdCheck(
    IPersonalizationDbContext Db,
    long FormId,
    IReadOnlyCollection<long> PropertyIds);

/// <summary>
/// Inputs for upserting property value cells.
/// </summary>
internal sealed record AudienceValueWrite(
    IPersonalizationDbContext Db,
    long TenantId,
    long MembershipId,
    IReadOnlyDictionary<long, string> Values);
