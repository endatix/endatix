using Endatix.Modules.Audience.Features.People;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Links;

/// <summary>
/// The one read of a membership's cells for a snapshot, used by every way a response opens (a
/// personalised link, a submission created on behalf), so their snapshots never differ in shape.
/// Unset cells and cells of deleted properties are left out by the query filters. The <see cref="MatchKeyProperty"/>
/// holds the participant's identifier, which wins over a stored cell. The tenant is stated as well as
/// filtered, so SQL Server can seek the tenant-first cell index.
/// </summary>
internal static class SnapshotCells
{
    public static async Task<List<SnapshotCell>> LoadAsync(
        IAudienceDbContext db,
        SnapshotMembership membership,
        CancellationToken cancellationToken)
    {
        List<SnapshotCell> cells = await StoredAsync(db, membership, cancellationToken);
        if (await MatchKeyCellAsync(db, membership, cancellationToken) is { } matchKey)
        {
            cells.RemoveAll(cell => cell.PropertyId == matchKey.PropertyId);
            cells.Add(matchKey);
        }

        return cells;
    }

    private static async Task<List<SnapshotCell>> StoredAsync(
        IAudienceDbContext db,
        SnapshotMembership membership,
        CancellationToken cancellationToken)
    {
        var rows = await (
            from value in db.PropertyValues
            join property in db.Properties on value.PropertyId equals property.Id
            where value.TenantId == membership.TenantId && value.MembershipId == membership.MembershipId
            select new { property.Id, property.VariableName, property.Name, property.DataType, value.Value })
            .ToListAsync(cancellationToken);
        return rows
            .Select(row => new SnapshotCell(row.Id, row.VariableName, row.Name, row.DataType, row.Value))
            .ToList();
    }

    /// <summary>One indexed read: the Property; the caller already holds the participant's form and identifier.</summary>
    private static async Task<SnapshotCell?> MatchKeyCellAsync(
        IAudienceDbContext db,
        SnapshotMembership membership,
        CancellationToken cancellationToken)
    {
        var property = await MatchKeyProperty.Of(db, membership.TenantId, membership.FormId)
            .Select(property => new { property.Id, property.VariableName, property.Name, property.DataType })
            .FirstOrDefaultAsync(cancellationToken);
        return property is null
            ? null
            : new SnapshotCell(property.Id, property.VariableName, property.Name, property.DataType, membership.Identifier);
    }
}

/// <summary>
/// The membership whose cells a snapshot reads, with the tenant that owns it, its form, and the participant's
/// identifier, which the match-key Property holds.
/// </summary>
internal readonly record struct SnapshotMembership(long TenantId, long MembershipId, long FormId, string Identifier);
