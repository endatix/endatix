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
    /// <summary>
    /// Checks that every property belongs to the form and that each value fits its property.
    /// </summary>
    public static async Task<Result> ValidateAsync(
        PropertyValuesCheck check,
        CancellationToken cancellationToken)
    {
        if (check.Values is null || check.Values.Count == 0)
        {
            return Result.Success();
        }

        List<long> ids = check.Values.Keys.ToList();
        Dictionary<long, Property> properties = await check.Db.Properties
            .AsNoTracking()
            .Where(property => property.FormId == check.FormId && ids.Contains(property.Id))
            .ToDictionaryAsync(property => property.Id, cancellationToken);

        return properties.Count == ids.Count
            ? ValueResult(check.Values, properties)
            : Result.Invalid(new ValidationError(
                "One or more property ids do not belong to this form."));
    }

    private static Result ValueResult(
        IReadOnlyDictionary<long, string> values,
        Dictionary<long, Property> properties)
    {
        foreach ((long propertyId, string value) in values)
        {
            string? error = PropertyValue.ValueError(value) ?? properties[propertyId].ValueError(value);
            if (error is not null)
            {
                return Result.Invalid(new ValidationError(error));
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Cells of the given memberships whose property is still active. A cell written while its
    /// property was being deleted stays out of every read.
    /// </summary>
    public static IQueryable<PropertyValue> ActiveCells(
        IAudienceDbContext db,
        IReadOnlyCollection<long> membershipIds) =>
        db.PropertyValues.AsNoTracking().Where(value =>
            membershipIds.Contains(value.MembershipId)
            && db.Properties.Any(property => property.Id == value.PropertyId));

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
/// Inputs for validating property values against a form's properties.
/// </summary>
internal sealed record PropertyValuesCheck(
    IAudienceDbContext Db,
    long FormId,
    IReadOnlyDictionary<long, string>? Values);

/// <summary>
/// Inputs for writing property value cells on one membership.
/// </summary>
internal sealed record PropertyValueWrite(
    IAudienceDbContext Db,
    long TenantId,
    long MembershipId,
    IReadOnlyDictionary<long, string> Values);
