using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.People;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Import;

/// <summary>
/// Stages every accepted row against the audience loaded up front, so an import costs a few
/// queries and one save instead of several round trips per row.
/// </summary>
internal sealed class ImportBatch(IAudienceDbContext db, ImportTarget target, ExistingAudience existing)
{
    public ImportTally Tally { get; } = new();

    public void Stage(ImportRow row)
    {
        Member member = existing.MembersByIdentifier.GetValueOrDefault(row.Identifier) ?? AddMember(row);
        if (!existing.MembershipsByMemberId.TryGetValue(member.Id, out Membership? membership))
        {
            AddMembership(member, row.Values);
            Tally.Created++;
            return;
        }

        if (row.Values.Count == 0)
        {
            Tally.Skipped++;
            return;
        }

        UpdateCells(membership, row.Values);
        Tally.Updated++;
    }

    private void UpdateCells(Membership membership, IReadOnlyDictionary<long, string> values)
    {
        IReadOnlyDictionary<long, PropertyValue> cells =
            existing.CellsByMembershipId.GetValueOrDefault(membership.Id) ?? new Dictionary<long, PropertyValue>();
        PropertyValuesWriter.Upsert(new PropertyValueWrite(db, target.TenantId, membership.Id, values), cells);
    }

    private Member AddMember(ImportRow row)
    {
        Member member = new(target.TenantId, row.Identifier, target.IdentifierKind);
        db.Members.Add(member);
        return member;
    }

    private void AddMembership(Member member, IReadOnlyDictionary<long, string> values)
    {
        Membership membership = new(target.TenantId, target.FormId, member.Id);
        db.Memberships.Add(membership);
        PropertyValuesWriter.AddAll(new PropertyValueWrite(db, target.TenantId, membership.Id, values));
    }
}

/// <summary>
/// The tenant, form and match key one import writes to.
/// </summary>
internal sealed record ImportTarget(long TenantId, long FormId, string IdentifierKind);

/// <summary>
/// Members, memberships and value cells that the import rows already match.
/// </summary>
internal sealed record ExistingAudience(
    IReadOnlyDictionary<string, Member> MembersByIdentifier,
    IReadOnlyDictionary<long, Membership> MembershipsByMemberId,
    IReadOnlyDictionary<long, Dictionary<long, PropertyValue>> CellsByMembershipId)
{
    public static async Task<ExistingAudience> LoadAsync(AudienceQuery query, IReadOnlyList<ImportRow> rows)
    {
        List<string> identifiers = rows.Select(row => row.Identifier).ToList();
        Dictionary<string, Member> members = await query.Db.Members
            .Where(member => member.TenantId == query.Target.TenantId && identifiers.Contains(member.Identifier))
            .ToDictionaryAsync(member => member.Identifier, StringComparer.Ordinal, query.CancellationToken);
        Dictionary<long, Membership> memberships = await LoadMembershipsAsync(query, members.Values);
        return new ExistingAudience(members, memberships, await LoadCellsAsync(query, memberships.Values));
    }

    private static Task<Dictionary<long, Membership>> LoadMembershipsAsync(
        AudienceQuery query,
        IEnumerable<Member> members)
    {
        List<long> memberIds = members.Select(member => member.Id).ToList();
        return query.Db.Memberships
            .Where(membership => membership.FormId == query.Target.FormId && memberIds.Contains(membership.MemberId))
            .ToDictionaryAsync(membership => membership.MemberId, query.CancellationToken);
    }

    private static async Task<IReadOnlyDictionary<long, Dictionary<long, PropertyValue>>> LoadCellsAsync(
        AudienceQuery query,
        IEnumerable<Membership> memberships)
    {
        List<long> membershipIds = memberships.Select(membership => membership.Id).ToList();
        List<PropertyValue> cells = await query.Db.PropertyValues
            .Where(cell => membershipIds.Contains(cell.MembershipId))
            .ToListAsync(query.CancellationToken);
        return cells
            .GroupBy(cell => cell.MembershipId)
            .ToDictionary(group => group.Key, group => group.ToDictionary(cell => cell.PropertyId));
    }
}

/// <summary>
/// Inputs for loading the audience an import touches.
/// </summary>
internal sealed record AudienceQuery(
    IAudienceDbContext Db,
    ImportTarget Target,
    CancellationToken CancellationToken);

/// <summary>
/// Counts of people created, updated and skipped by one import.
/// </summary>
internal sealed class ImportTally
{
    public int Created { get; set; }

    public int Updated { get; set; }

    public int Skipped { get; set; }
}
