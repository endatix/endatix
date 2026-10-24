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
            string? error = ValueError(properties[propertyId], value);
            if (error is not null)
            {
                return Result.Invalid(new ValidationError(error));
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Why <paramref name="value"/> cannot be stored for <paramref name="property"/>: the cell rule
    /// (length) first, then the property's type and choices. The one rule every writer checks.
    /// </summary>
    public static string? ValueError(Property property, string value) =>
        PropertyValue.ValueError(value) ?? property.ValueError(value);

    /// <summary>
    /// Cells of the given memberships for the given live Properties, which the caller has read: a cell written while
    /// its property was being deleted stays out of every read. One table and both keys of <c>IX_PropertyValues_Cell</c>,
    /// so the only plan is a seek per membership. An <c>EXISTS</c> on Properties here let both providers drive from the
    /// Properties and scan every cell of the tenant, or the whole table on SQL Server, for 20 participants (#1376).
    /// </summary>
    public static IQueryable<PropertyValue> ActiveCells(IAudienceDbContext db, ActiveCellsKey key) =>
        db.PropertyValues.AsNoTracking().Where(value =>
            value.TenantId == key.TenantId
            && key.MembershipIds.Contains(value.MembershipId)
            && key.PropertyIds.Contains(value.PropertyId));

    /// <summary>
    /// Stages value cells for a membership that has none yet. Empty values add nothing. Does not save.
    /// </summary>
    public static void AddAll(PropertyValueWrite write) =>
        Upsert(write, new Dictionary<long, PropertyValue>());

    /// <summary>
    /// Updates, clears or adds each cell. Does not save.
    /// </summary>
    public static async Task UpsertAsync(
        PropertyValueWrite write,
        CancellationToken cancellationToken)
    {
        Dictionary<long, PropertyValue> existing = await LoadCellsAsync(write, cancellationToken);
        Upsert(write, existing);
    }

    /// <summary>
    /// The values a person ends up with after <paramref name="values"/> is written: empty ones
    /// clear their cell, so they are left out.
    /// </summary>
    public static Dictionary<long, string> Stored(IReadOnlyDictionary<long, string> values) =>
        values.Where(pair => pair.Value.Length > 0).ToDictionary(pair => pair.Key, pair => pair.Value);

    /// <summary>
    /// Updates, clears or adds each cell against cells already loaded (keyed by property id), as
    /// the CSV import does. An empty value soft-deletes the cell, so a read never sees an empty
    /// cell. Cells are written in property-id order, so two requests touching the same cells lock
    /// them in the same order and cannot deadlock. Does not save.
    /// </summary>
    public static void Upsert(PropertyValueWrite write, IReadOnlyDictionary<long, PropertyValue> existing)
    {
        foreach ((long propertyId, string value) in write.Values.OrderBy(pair => pair.Key))
        {
            PropertyValue? cell = existing.GetValueOrDefault(propertyId);
            if (cell is null)
            {
                AddCellUnlessEmpty(write, propertyId, value);
            }
            else if (value.Length == 0)
            {
                cell.Delete();
            }
            else
            {
                cell.SetValue(value);
            }
        }
    }

    private static Task<Dictionary<long, PropertyValue>> LoadCellsAsync(
        PropertyValueWrite write,
        CancellationToken cancellationToken)
    {
        HashSet<long> propertyIds = write.Values.Keys.ToHashSet();
        return write.Db.PropertyValues
            .Where(value => value.TenantId == write.TenantId
                && value.MembershipId == write.MembershipId
                && propertyIds.Contains(value.PropertyId))
            .ToDictionaryAsync(value => value.PropertyId, cancellationToken);
    }

    private static void AddCellUnlessEmpty(PropertyValueWrite write, long propertyId, string value)
    {
        if (value.Length > 0)
        {
            write.Db.PropertyValues.Add(new PropertyValue(new PropertyValueCreateArgs(
                write.TenantId, write.MembershipId, propertyId, value)));
        }
    }
}

/// <summary>The memberships whose cells to read, and the form's live Properties to read them for.</summary>
internal sealed record ActiveCellsKey(
    long TenantId,
    IReadOnlyCollection<long> MembershipIds,
    IReadOnlyCollection<long> PropertyIds);

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
