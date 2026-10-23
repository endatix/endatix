using Endatix.Core.Abstractions;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.People;
using Endatix.Modules.Audience.Features.Properties;
using Endatix.Modules.Audience.Features.Settings;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Variables;

/// <summary>
/// The only database read behind a form's variables: the Properties in tab order, the first
/// <see cref="PresetLimit"/> Test participants by when they were added, their live cells, and the coverage counts; or
/// one participant, real or test, with their live cells. Removed participants and deleted Properties are left out by
/// the query filters. The tenant is stated as well as filtered, so SQL Server seeks the tenant-first indexes.
/// </summary>
internal sealed class AudienceVariablesReader(IAudienceDbContext db, IValueNormalizer normalizer)
{
    /// <summary>The most presets one read returns; CSV import can add thousands of Test participants.</summary>
    public const int PresetLimit = 20;

    private static readonly IReadOnlyDictionary<(long PropertyId, bool IsTest), int> NothingFilled =
        new Dictionary<(long PropertyId, bool IsTest), int>();

    /// <summary>
    /// Up to seven statements: the Properties, participants by test flag, filled cells by Property (all, then the Test
    /// participants' ids and their cells), then the first Test participants and their cells. Stops after the second
    /// when the form has no Properties or no participants, and skips the test counts and presets when it has no Test
    /// participants.
    /// </summary>
    public Task<FormAudience> ReadAsync(FormKey key, CancellationToken cancellationToken) =>
        ReadAsync(key, [], cancellationToken);

    /// <summary>
    /// As <see cref="ReadAsync(FormKey, CancellationToken)"/>, with the Test participants named by
    /// <paramref name="savedKeys"/> (normalized identifiers) among the presets too, so a write-back reply holds every
    /// preset it saved, also past the first <see cref="PresetLimit"/>. One statement more when any are named.
    /// </summary>
    public async Task<FormAudience> ReadAsync(
        FormKey key,
        IReadOnlyCollection<string> savedKeys,
        CancellationToken cancellationToken)
    {
        List<Property> properties = await PropertiesAsync(key, cancellationToken);
        ParticipantCounts participants = await CountParticipantsAsync(key, cancellationToken);
        if (properties.Count == 0 || participants is { Test: 0, Real: 0 })
        {
            return new FormAudience(properties, [], participants, NothingFilled);
        }

        IReadOnlyDictionary<(long PropertyId, bool IsTest), int> filled =
            await CountFilledAsync(key, properties, participants, cancellationToken);
        List<PresetSource> testParticipants = participants.Test == 0
            ? []
            : await TestParticipantsAsync(key, savedKeys, cancellationToken);
        return new FormAudience(properties, testParticipants, participants, filled);
    }

    /// <summary>The first Test participants by when they were added, and those named by <paramref name="savedKeys"/>.</summary>
    private async Task<List<PresetSource>> TestParticipantsAsync(
        FormKey key,
        IReadOnlyCollection<string> savedKeys,
        CancellationToken cancellationToken)
    {
        IQueryable<Membership> first = TestMemberships(key).OrderBy(membership => membership.Id).Take(PresetLimit);
        if (savedKeys.Count == 0)
        {
            return await ParticipantsAsync(key, first, cancellationToken);
        }

        List<long> ids = await first.Select(membership => membership.Id).ToListAsync(cancellationToken);
        ids.AddRange(await Keyed(key, TestMemberships(key), savedKeys)
            .Select(membership => membership.MembershipId)
            .ToListAsync(cancellationToken));
        HashSet<long> presetIds = ids.ToHashSet();
        return await ParticipantsAsync(
            key, Memberships(key).Where(membership => presetIds.Contains(membership.Id)), cancellationToken);
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

    /// <summary>
    /// The form's Properties and the participants on it, real or test, named by <paramref name="identifiers"/>, matched
    /// as the tenant's match key matches (an email ignores case). Invalid when the match key refuses an identifier.
    /// </summary>
    public async Task<Result<KeyedAudience>> ReadByIdentifiersAsync(
        FormKey key,
        IReadOnlyCollection<string> identifiers,
        CancellationToken cancellationToken)
    {
        string identifierKind = await IdentifierKindReader.GetAsync(db, key.TenantId, cancellationToken);
        if (identifiers.Select(identifier => Member.IdentifierError(identifier, identifierKind))
                .FirstOrDefault(error => error is not null) is { } identifierError)
        {
            return Result.Invalid(new ValidationError(identifierError));
        }

        Dictionary<string, string> keys = identifiers.Distinct().ToDictionary(
            identifier => identifier,
            identifier => Member.Normalize(identifier, identifierKind, normalizer));
        return new KeyedAudience(
            await PropertiesAsync(key, cancellationToken),
            await OnFormAsync(key, keys.Values.ToHashSet(), cancellationToken),
            keys);
    }

    /// <summary>Two statements; one when none match.</summary>
    private async Task<Dictionary<string, OnForm>> OnFormAsync(
        FormKey key,
        HashSet<string> keys,
        CancellationToken cancellationToken)
    {
        List<KeyedMembership> matched = await Keyed(key, Memberships(key), keys).ToListAsync(cancellationToken);
        ILookup<long, CellRow> cells = matched.Count == 0
            ? Enumerable.Empty<CellRow>().ToLookup(cell => cell.MembershipId)
            : await CellsAsync(key, matched.ConvertAll(participant => participant.MembershipId), cancellationToken);
        return matched.ToDictionary(
            participant => participant.Key,
            participant => new OnForm(
                participant.MembershipId,
                participant.IsTest,
                cells[participant.MembershipId].ToDictionary(cell => cell.PropertyId, cell => cell.Value)));
    }

    private Task<List<Property>> PropertiesAsync(FormKey key, CancellationToken cancellationToken) =>
        db.Properties.AsNoTracking()
            .Where(property => property.TenantId == key.TenantId && property.FormId == key.FormId)
            .InTabOrder()
            .ToListAsync(cancellationToken);

    /// <summary>
    /// The form's live memberships. No member test: a live membership always has a live Member. The only Member delete,
    /// <c>RetireMembersAsync</c> on an identifier-kind change, runs only while the tenant has no live memberships. The
    /// EXISTS on Members the people list keeps would only give the planner another join to get wrong; if that guard
    /// ever loosens, add it back here too.
    /// </summary>
    private IQueryable<Membership> Memberships(FormKey key) =>
        db.Memberships.AsNoTracking()
            .Where(membership => membership.TenantId == key.TenantId && membership.FormId == key.FormId);

    private IQueryable<Membership> TestMemberships(FormKey key) =>
        Memberships(key).Where(membership => membership.IsTest);

    private async Task<ParticipantCounts> CountParticipantsAsync(FormKey key, CancellationToken cancellationToken)
    {
        Dictionary<bool, int> counts = await Memberships(key)
            .GroupBy(membership => membership.IsTest)
            .Select(group => new { IsTest = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.IsTest, group => group.Count, cancellationToken);
        return new ParticipantCounts(counts.GetValueOrDefault(true), counts.GetValueOrDefault(false));
    }

    /// <summary>
    /// Filled cells per live Property: every cell, then the Test participants' cells, in two GROUP BYs with no join
    /// between cells and memberships. Removing a person or a Property deletes its cells, so real = all − test. The join
    /// this replaced let PostgreSQL loop the form's memberships over <c>IX_PropertyValues_Property</c>, a scan of every
    /// cell per participant, and time out at 5,000 × 50 when its statistics were off (CI). Now the first reads one index
    /// range per Property and the second reaches cells only through the test memberships (<c>IX_PropertyValues_Cell</c>).
    /// The first filters by the live Property ids just read, which is what <see cref="PropertyValuesWriter.ActiveCells(IAudienceDbContext, long, IReadOnlyCollection{long})"/>
    /// tests with an EXISTS. The second reads the Test participants' cells by their ids, with no Property predicate
    /// (<see cref="CountTestCellsAsync"/>); a deleted Property's cells it counts are dropped here, since only the live
    /// ids are read back. The statements share no snapshot, so a person added or removed between them could push a
    /// count past the participants or below zero; each count is clamped to <c>0..participants</c>.
    /// </summary>
    private async Task<IReadOnlyDictionary<(long PropertyId, bool IsTest), int>> CountFilledAsync(
        FormKey key,
        List<Property> properties,
        ParticipantCounts participants,
        CancellationToken cancellationToken)
    {
        List<long> propertyIds = properties.ConvertAll(property => property.Id);
        Dictionary<long, int> all = await CountByPropertyAsync(
            db.PropertyValues.Where(cell => cell.TenantId == key.TenantId && propertyIds.Contains(cell.PropertyId)),
            cancellationToken);
        Dictionary<long, int> test = participants.Test == 0
            ? []
            : await CountTestCellsAsync(key, cancellationToken);
        return propertyIds
            .SelectMany(id =>
            {
                int filledTest = Math.Clamp(test.GetValueOrDefault(id), 0, participants.Test);
                int filledReal = Math.Clamp(all.GetValueOrDefault(id) - filledTest, 0, participants.Real);
                return new[] { ((id, true), filledTest), ((id, false), filledReal) };
            })
            .ToDictionary(entry => entry.Item1, entry => entry.Item2);
    }

    /// <summary>
    /// The Test participants' ids first, then their cells by those ids: two statements and no join, so no plan can loop
    /// one side over the other. A subquery here (with or without a Property predicate) let PostgreSQL scan the tenant's
    /// cells and run the membership subquery per cell, and CI timed out at 30 s.
    /// </summary>
    private async Task<Dictionary<long, int>> CountTestCellsAsync(FormKey key, CancellationToken cancellationToken)
    {
        List<long> testMemberships = await TestMemberships(key)
            .Select(membership => membership.Id)
            .ToListAsync(cancellationToken);
        return await CountByPropertyAsync(
            db.PropertyValues.Where(cell =>
                cell.TenantId == key.TenantId && testMemberships.Contains(cell.MembershipId)),
            cancellationToken);
    }

    /// <summary>Cells per property id.</summary>
    private static Task<Dictionary<long, int>> CountByPropertyAsync(
        IQueryable<PropertyValue> cells,
        CancellationToken cancellationToken) =>
        cells.AsNoTracking()
            .GroupBy(cell => cell.PropertyId)
            .Select(group => new { PropertyId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.PropertyId, group => group.Count, cancellationToken);

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

    /// <summary>The memberships whose member's normalized identifier is one of <paramref name="keys"/>.</summary>
    private IQueryable<KeyedMembership> Keyed(
        FormKey key,
        IQueryable<Membership> memberships,
        IReadOnlyCollection<string> keys) =>
        memberships.Join(
            db.Members.Where(member => member.TenantId == key.TenantId && keys.Contains(member.NormalizedIdentifier)),
            membership => membership.MemberId,
            member => member.Id,
            (membership, member) => new KeyedMembership(membership.Id, membership.IsTest, member.NormalizedIdentifier));

    /// <summary>
    /// The live cells of the memberships just read, by membership id. By id, not by the membership query again: a
    /// participant removed between the two statements keeps the values they were read with, never an empty preset.
    /// At most <see cref="PresetLimit"/> ids, plus those a write-back saved.
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

    private sealed record KeyedMembership(long MembershipId, bool IsTest, string Key);

    private sealed record CellRow(long MembershipId, long PropertyId, string Value);
}

/// <summary>One form of one tenant.</summary>
internal readonly record struct FormKey(long TenantId, long FormId);

/// <summary>
/// A form's Properties in tab order, the participants on it a write names by normalized identifier, and each named
/// identifier's normalized form.
/// </summary>
internal sealed record KeyedAudience(
    IReadOnlyList<Property> Properties,
    IReadOnlyDictionary<string, OnForm> OnForm,
    IReadOnlyDictionary<string, string> Keys);
