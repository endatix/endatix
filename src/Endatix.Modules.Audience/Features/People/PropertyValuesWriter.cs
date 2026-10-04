using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.People;

/// <summary>
/// Shared write path for property value cells on a membership.
/// </summary>
internal static class PropertyValuesWriter
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
        int known = await check.Db.Properties.CountAsync(
            property => property.FormId == check.FormId && ids.Contains(property.Id),
            cancellationToken);

        return known == ids.Count
            ? Result.Success()
            : Result.Invalid(new ValidationError(
                "One or more property ids do not belong to this form."));
    }

    /// <summary>
    /// Stages value cells for a membership that has none yet. Does not save.
    /// </summary>
    public static void AddAll(PropertyValueWrite write)
    {
        foreach ((long propertyId, string value) in write.Values)
        {
            AddCell(write, propertyId, value);
        }
    }

    /// <summary>
    /// Updates existing cells and stages missing ones. Does not save.
    /// </summary>
    public static async Task UpsertAsync(
        PropertyValueWrite write,
        CancellationToken cancellationToken)
    {
        Dictionary<long, PropertyValue> existing = await LoadCellsAsync(write, cancellationToken);
        foreach ((long propertyId, string value) in write.Values)
        {
            if (existing.TryGetValue(propertyId, out PropertyValue? cell))
            {
                cell.SetValue(value);
                continue;
            }

            AddCell(write, propertyId, value);
        }
    }

    private static Task<Dictionary<long, PropertyValue>> LoadCellsAsync(
        PropertyValueWrite write,
        CancellationToken cancellationToken)
    {
        HashSet<long> propertyIds = write.Values.Keys.ToHashSet();
        return write.Db.PropertyValues
            .Where(value => value.MembershipId == write.MembershipId
                && propertyIds.Contains(value.PropertyId))
            .ToDictionaryAsync(value => value.PropertyId, cancellationToken);
    }

    private static void AddCell(PropertyValueWrite write, long propertyId, string value) =>
        write.Db.PropertyValues.Add(new PropertyValue(new PropertyValueCreateArgs(
            write.TenantId, write.MembershipId, propertyId, value)));
}

/// <summary>
/// Inputs for validating property ids against a form.
/// </summary>
internal sealed record PropertyIdCheck(
    IAudienceDbContext Db,
    long FormId,
    IReadOnlyCollection<long> PropertyIds);

/// <summary>
/// Inputs for writing property value cells on one membership.
/// </summary>
internal sealed record PropertyValueWrite(
    IAudienceDbContext Db,
    long TenantId,
    long MembershipId,
    IReadOnlyDictionary<long, string> Values);
