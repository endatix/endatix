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

    /// <summary>
    /// A test import never writes to a person already on the form, so real people's data stays as it was.
    /// </summary>
    public void Stage(ImportRow row)
    {
        Member member = existing.MembersByMatchKey.GetValueOrDefault(row.NormalizedIdentifier) ?? AddMember(row);
        if (!existing.MembershipsByMemberId.TryGetValue(member.Id, out Membership? membership))
        {
            AddMembership(member, row.Values);
            Tally.Created++;
            return;
        }

        if (!target.IsTest && UpdateIfChanged(membership, row.Values))
        {
            Tally.Updated++;
        }
        else
        {
            Tally.Skipped++;
        }
    }

    /// <summary>
    /// Writes the row's values when any differs from the stored cell, so re-importing the same
    /// file reports its people as unchanged and leaves their cells untouched.
    /// </summary>
    private bool UpdateIfChanged(Membership membership, IReadOnlyDictionary<long, string> values)
    {
        IReadOnlyDictionary<long, PropertyValue> cells =
            existing.CellsByMembershipId.GetValueOrDefault(membership.Id) ?? new Dictionary<long, PropertyValue>();
        if (!Changes(values, cells))
        {
            return false;
        }

        PropertyValuesWriter.Upsert(new PropertyValueWrite(db, membership, values), cells);
        return true;
    }

    private static bool Changes(
        IReadOnlyDictionary<long, string> values,
        IReadOnlyDictionary<long, PropertyValue> cells) =>
        values.Any(pair => cells.GetValueOrDefault(pair.Key)?.Value != pair.Value);

    private Member AddMember(ImportRow row)
    {
        Member member = new(new MemberCreateArgs(
            target.TenantId, row.Identifier, target.MatchKey.IdentifierKind, target.MatchKey.Normalizer));
        db.Members.Add(member);
        return member;
    }

    private void AddMembership(Member member, IReadOnlyDictionary<long, string> values)
    {
        Membership membership = new(new MembershipCreateArgs(target.TenantId, target.FormId, member.Id, target.IsTest));
        db.Memberships.Add(membership);
        PropertyValuesWriter.AddAll(new PropertyValueWrite(db, membership, values));
    }
}

/// <summary>
/// The tenant, form and match key one import writes to, and whether the memberships it adds are test persons.
/// </summary>
internal sealed record ImportTarget(long TenantId, long FormId, ImportMatchKey MatchKey, bool IsTest);

/// <summary>
/// Members, memberships and value cells that the import rows already match.
/// </summary>
internal sealed record ExistingAudience(
    IReadOnlyDictionary<string, Member> MembersByMatchKey,
    IReadOnlyDictionary<long, Membership> MembershipsByMemberId,
    IReadOnlyDictionary<long, Dictionary<long, PropertyValue>> CellsByMembershipId)
{
    public static async Task<ExistingAudience> LoadAsync(AudienceQuery query, IReadOnlyList<ImportRow> rows)
    {
        List<string> matchKeys = rows.Select(row => row.NormalizedIdentifier).ToList();
        Dictionary<string, Member> members = await query.Db.Members
            .Where(member => member.TenantId == query.Target.TenantId
                && matchKeys.Contains(member.NormalizedIdentifier))
            .ToDictionaryAsync(member => member.NormalizedIdentifier, StringComparer.Ordinal, query.CancellationToken);
        Dictionary<long, Membership> memberships = await LoadMembershipsAsync(query, members.Values);
        return new ExistingAudience(members, memberships, await LoadCellsAsync(query, memberships.Values));
    }

    private static Task<Dictionary<long, Membership>> LoadMembershipsAsync(
        AudienceQuery query,
        IEnumerable<Member> members)
    {
        List<long> memberIds = members.Select(member => member.Id).ToList();
        return query.Db.Memberships
            .Where(membership => membership.TenantId == query.Target.TenantId
                && membership.FormId == query.Target.FormId
                && memberIds.Contains(membership.MemberId))
            .ToDictionaryAsync(membership => membership.MemberId, query.CancellationToken);
    }

    private static async Task<IReadOnlyDictionary<long, Dictionary<long, PropertyValue>>> LoadCellsAsync(
        AudienceQuery query,
        IEnumerable<Membership> memberships)
    {
        List<long> membershipIds = memberships.Select(membership => membership.Id).ToList();
        List<PropertyValue> cells = await query.Db.PropertyValues
            .Where(cell => cell.TenantId == query.Target.TenantId && membershipIds.Contains(cell.MembershipId))
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
