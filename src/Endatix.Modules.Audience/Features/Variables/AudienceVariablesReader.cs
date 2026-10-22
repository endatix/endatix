using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.People;
using Endatix.Modules.Audience.Features.Properties;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Variables;

/// <summary>
/// The only database read behind a form's variables: the Properties in tab order, the first
/// <see cref="PresetLimit"/> Test participants by when they were added, their live cells, and the coverage counts; or
/// one participant, real or test, with their live cells. Removed participants and deleted Properties are left out by
/// the query filters. The tenant is stated as well as filtered, so SQL Server seeks the tenant-first indexes.
/// </summary>
internal sealed class AudienceVariablesReader(IAudienceDbContext db)
{
    /// <summary>The most presets one read returns; CSV import can add thousands of Test participants.</summary>
    public const int PresetLimit = 20;

    private static readonly IReadOnlyDictionary<(long PropertyId, bool IsTest), int> NothingFilled =
        new Dictionary<(long PropertyId, bool IsTest), int>();

    /// <summary>
    /// Up to five statements: the Properties, participants by test flag, filled cells by Property and test flag, then
    /// the first Test participants and their cells. Stops after the second when the form has no Properties or no
    /// participants.
    /// </summary>
    public async Task<FormAudience> ReadAsync(FormKey key, CancellationToken cancellationToken)
    {
        List<Property> properties = await PropertiesAsync(key, cancellationToken);
        ParticipantCounts participants = await CountParticipantsAsync(key, cancellationToken);
        if (properties.Count == 0 || participants is { Test: 0, Real: 0 })
        {
            return new FormAudience(properties, [], participants, NothingFilled);
        }

        IReadOnlyDictionary<(long PropertyId, bool IsTest), int> filled =
            await CountFilledAsync(key, properties, cancellationToken);
        IQueryable<Membership> firstTestMemberships = Memberships(key)
            .Where(membership => membership.IsTest)
            .OrderBy(membership => membership.Id)
            .Take(PresetLimit);
        List<PresetSource> testParticipants = participants.Test == 0
            ? []
            : await ParticipantsAsync(key, firstTestMemberships, cancellationToken);
        return new FormAudience(properties, testParticipants, participants, filled);
    }

    /// <summary>
    /// One participant on the form by audience member id with the form's Properties, or <see langword="null"/> when
    /// they are not on it or were removed. Three statements: the participant, their cells, the Properties.
    /// </summary>
    public async Task<ParticipantAudience?> ReadOneAsync(
        FormKey key,
        long memberId,
        CancellationToken cancellationToken)
    {
        List<PresetSource> participant = await ParticipantsAsync(
            key, Memberships(key).Where(membership => membership.MemberId == memberId), cancellationToken);
        return participant.Count == 0
            ? null
            : new ParticipantAudience(await PropertiesAsync(key, cancellationToken), participant[0]);
    }

    private Task<List<Property>> PropertiesAsync(FormKey key, CancellationToken cancellationToken) =>
        db.Properties.AsNoTracking()
            .Where(property => property.TenantId == key.TenantId && property.FormId == key.FormId)
            .InTabOrder()
            .ToListAsync(cancellationToken);

    /// <summary>The form's memberships whose member is live, as the people list keeps them.</summary>
    private IQueryable<Membership> Memberships(FormKey key) =>
        db.Memberships.AsNoTracking()
            .Where(membership => membership.TenantId == key.TenantId
                && membership.FormId == key.FormId
                && db.Members.Any(member => member.Id == membership.MemberId));

    private async Task<ParticipantCounts> CountParticipantsAsync(FormKey key, CancellationToken cancellationToken)
    {
        Dictionary<bool, int> counts = await Memberships(key)
            .GroupBy(membership => membership.IsTest)
            .Select(group => new { IsTest = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.IsTest, group => group.Count, cancellationToken);
        return new ParticipantCounts(counts.GetValueOrDefault(true), counts.GetValueOrDefault(false));
    }

    /// <summary>
    /// One GROUP BY over the cells of the form's live Properties, which seeks <c>IX_PropertyValues_Property</c>
    /// (tenant, property), joined to the live memberships for the test flag. A cell exists only when it holds a value.
    /// </summary>
    private async Task<IReadOnlyDictionary<(long PropertyId, bool IsTest), int>> CountFilledAsync(
        FormKey key,
        List<Property> properties,
        CancellationToken cancellationToken)
    {
        List<long> propertyIds = properties.ConvertAll(property => property.Id);
        var rows = await db.PropertyValues.AsNoTracking()
            .Where(cell => cell.TenantId == key.TenantId && propertyIds.Contains(cell.PropertyId))
            .Join(
                Memberships(key),
                cell => cell.MembershipId,
                membership => membership.Id,
                (cell, membership) => new { cell.PropertyId, membership.IsTest })
            .GroupBy(cell => new { cell.PropertyId, cell.IsTest })
            .Select(group => new { group.Key.PropertyId, group.Key.IsTest, Count = group.Count() })
            .ToListAsync(cancellationToken);
        return rows.ToDictionary(row => (row.PropertyId, row.IsTest), row => row.Count);
    }

    private async Task<List<PresetSource>> ParticipantsAsync(
        FormKey key,
        IQueryable<Membership> memberships,
        CancellationToken cancellationToken)
    {
        List<NamedMembership> participants = await Named(key, memberships).ToListAsync(cancellationToken);
        if (participants.Count == 0)
        {
            return [];
        }

        ILookup<long, CellRow> cells = await CellsAsync(
            key, participants.ConvertAll(participant => participant.MembershipId), cancellationToken);
        return participants.ConvertAll(participant => new PresetSource(
            participant.Identifier,
            cells[participant.MembershipId].ToDictionary(cell => cell.PropertyId, cell => cell.Value)));
    }

    private IQueryable<NamedMembership> Named(FormKey key, IQueryable<Membership> memberships) =>
        memberships.Join(
            db.Members.Where(member => member.TenantId == key.TenantId),
            membership => membership.MemberId,
            member => member.Id,
            (membership, member) => new NamedMembership(membership.Id, member.Identifier));

    /// <summary>
    /// The live cells of the memberships just read, by membership id. By id, not by the membership query again: a
    /// participant removed between the two statements keeps the values they were read with, never an empty preset.
    /// At most <see cref="PresetLimit"/> ids.
    /// </summary>
    private async Task<ILookup<long, CellRow>> CellsAsync(
        FormKey key,
        IReadOnlyCollection<long> membershipIds,
        CancellationToken cancellationToken) =>
        (await PropertyValuesWriter
            .ActiveCells(db, key.TenantId, membershipIds)
            .Select(cell => new CellRow(cell.MembershipId, cell.PropertyId, cell.Value))
            .ToListAsync(cancellationToken))
        .ToLookup(cell => cell.MembershipId);

    private sealed record NamedMembership(long MembershipId, string Identifier);

    private sealed record CellRow(long MembershipId, long PropertyId, string Value);
}

/// <summary>One form of one tenant.</summary>
internal readonly record struct FormKey(long TenantId, long FormId);
