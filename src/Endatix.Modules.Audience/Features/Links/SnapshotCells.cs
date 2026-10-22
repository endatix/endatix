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

    /// <summary>
    /// Two indexed reads: the membership's form and identifier, then the Property. A participant removed meanwhile gets
    /// none here; the open finds them gone anyway.
    /// </summary>
    private static async Task<SnapshotCell?> MatchKeyCellAsync(
        IAudienceDbContext db,
        SnapshotMembership membership,
        CancellationToken cancellationToken)
    {
        var participant = await FormParticipants
            .Of(db, db.Memberships.AsNoTracking().Where(row =>
                row.TenantId == membership.TenantId && row.Id == membership.MembershipId))
            .Select(row => new { row.Membership.FormId, row.Member.Identifier })
            .FirstOrDefaultAsync(cancellationToken);
        var property = participant is null
            ? null
            : await MatchKeyProperty.Of(db, membership.TenantId, participant.FormId)
                .Select(property => new { property.Id, property.VariableName, property.Name, property.DataType })
                .FirstOrDefaultAsync(cancellationToken);
        return property is null
            ? null
            : new SnapshotCell(
                property.Id, property.VariableName, property.Name, property.DataType, participant!.Identifier);
    }
}

/// <summary>The membership whose cells a snapshot reads, with the tenant that owns it.</summary>
internal readonly record struct SnapshotMembership(long TenantId, long MembershipId);
