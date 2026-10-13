using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Links;

/// <summary>
/// The one read of a membership's cells for a snapshot, used by every way a response opens (a
/// personalised link, a submission created on behalf), so their snapshots never differ in shape.
/// Unset cells and cells of deleted properties are left out by the query filters. The tenant is stated as well as
/// filtered, so SQL Server can seek the tenant-first cell index.
/// </summary>
internal static class SnapshotCells
{
    public static async Task<List<SnapshotCell>> LoadAsync(
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
}

/// <summary>The membership whose cells a snapshot reads, with the tenant that owns it.</summary>
internal readonly record struct SnapshotMembership(long TenantId, long MembershipId);
