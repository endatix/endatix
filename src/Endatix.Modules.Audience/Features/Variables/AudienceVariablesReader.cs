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
/// The only database read behind a form's variables: the Properties in tab order, the first <see cref="PresetLimit"/>
/// participants (Test participants first, each group by when they were added), their live cells, and the coverage
/// counts; or
/// one participant, real or test, with their live cells. Removed participants and deleted Properties are left out by
/// the query filters. The tenant is stated as well as filtered, so SQL Server seeks the tenant-first indexes; the
/// member join by key drops it (<see cref="FormParticipants.Of"/>).
/// </summary>
internal sealed class AudienceVariablesReader(IAudienceDbContext db, IValueNormalizer normalizer)
{
    /// <summary>The most presets one read returns; CSV import can add thousands of participants.</summary>
    public const int PresetLimit = 20;

    private static readonly IReadOnlyDictionary<(long PropertyId, bool IsTest), int> NothingFilled =
        new Dictionary<(long PropertyId, bool IsTest), int>();

    /// <summary>
    /// Up to eight statements: the Properties, participants by test flag, filled cells by Property (all, then the Test
    /// participants' ids and their cells), the match-key Property, then the first participants and their cells
    /// (<see cref="ParticipantValues"/>). Stops after the second when the form has no Properties or no participants,
    /// and skips the test counts when it has no Test participants.
    /// </summary>
    public Task<FormAudience> ReadAsync(FormKey key, CancellationToken cancellationToken) =>
        ReadAsync(key, [], cancellationToken);

    /// <summary>
    /// As <see cref="ReadAsync(FormKey, CancellationToken)"/>, with the participants named by
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
        MatchKey? matchKey = await MatchKeyProperty.FindAsync(db, new(key.TenantId, key.FormId), cancellationToken);
        return new FormAudience(
            properties,
            await FirstParticipantsAsync(
                new ValuesScope(key, matchKey, IdsOf(properties)), savedKeys, cancellationToken),
            participants,
            MatchKeyProperty.Filled(filled, matchKey, (participants.Test, participants.Real)));
    }

    /// <summary>
    /// The first <see cref="PresetLimit"/> participants (Test participants first, each group by when added), and those
    /// named by <paramref name="savedKeys"/>.
    /// </summary>
    private async Task<List<PresetSource>> FirstParticipantsAsync(
        ValuesScope scope,
        IReadOnlyCollection<string> savedKeys,
        CancellationToken cancellationToken)
    {
        IQueryable<Membership> first = Memberships(scope.Key)
            .OrderByDescending(membership => membership.IsTest)
            .ThenBy(membership => membership.Id)
            .Take(PresetLimit);
        if (savedKeys.Count == 0)
        {
            return await ParticipantsAsync(scope, first, cancellationToken);
        }

        List<long> ids = await first.Select(membership => membership.Id).ToListAsync(cancellationToken);
        ids.AddRange(await Keyed(scope.Key, Memberships(scope.Key), savedKeys)
            .Select(membership => membership.MembershipId)
            .ToListAsync(cancellationToken));
        HashSet<long> presetIds = ids.ToHashSet();
        return await ParticipantsAsync(
            scope, Memberships(scope.Key).Where(membership => presetIds.Contains(membership.Id)), cancellationToken);
    }

    /// <summary>
    /// One participant on the form by audience member id with the form's Properties, or <see langword="null"/> when
    /// they are not on it or were removed. Four statements: the participant, the match-key Property, the Properties,
    /// their cells; one when they are not on the form.
    /// </summary>
    public async Task<ParticipantAudience?> ReadOneAsync(
        FormKey key,
        long memberId,
        CancellationToken cancellationToken)
    {
        List<NamedMembership> named = await Named(
                FormParticipants.ByMember(db, new ParticipantKey(key.TenantId, key.FormId, memberId)))
            .ToListAsync(cancellationToken);
        if (named.Count == 0)
        {
            return null;
        }

        MatchKey? matchKey = await MatchKeyProperty.FindAsync(db, new(key.TenantId, key.FormId), cancellationToken);
        List<Property> properties = await PropertiesAsync(key, cancellationToken);
        List<PresetSource> participant = await PresetsAsync(
            new ValuesScope(key, matchKey, IdsOf(properties)), named, cancellationToken);
        return new ParticipantAudience(properties, participant[0]);
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

        Dictionary<string, string> keys = KeysOf(identifiers, identifierKind);
        List<Property> properties = await PropertiesAsync(key, cancellationToken);
        ValuesScope scope = new(key, MatchKeyProperty.In(properties, identifierKind), IdsOf(properties));
        return new KeyedAudience(
            properties, await OnFormAsync(scope, keys.Values.ToHashSet(), cancellationToken), keys, scope.MatchKey);
    }

    /// <summary>Each identifier and its normalized form under the tenant's match key.</summary>
    private Dictionary<string, string> KeysOf(IReadOnlyCollection<string> identifiers, string identifierKind) =>
        identifiers.Distinct().ToDictionary(
            identifier => identifier,
            identifier => Member.Normalize(identifier, identifierKind, normalizer));

    /// <summary>Two statements; one when none match.</summary>
    private async Task<Dictionary<string, OnForm>> OnFormAsync(
        ValuesScope scope,
        HashSet<string> keys,
        CancellationToken cancellationToken)
    {
        List<KeyedMembership> matched =
            await Keyed(scope.Key, Memberships(scope.Key), keys).ToListAsync(cancellationToken);
        Dictionary<long, Dictionary<long, string>> values = matched.Count == 0
            ? []
            : await CellsAsync(scope, matched.Select(row => (row.MembershipId, row.Identifier)), cancellationToken);
        return matched.ToDictionary(
            participant => participant.Key,
            participant => new OnForm(
                participant.MembershipId,
                participant.IsTest,
                values.GetValueOrDefault(participant.MembershipId) ?? []));
    }

    private static List<long> IdsOf(List<Property> properties) => properties.ConvertAll(property => property.Id);

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
    private IQueryable<Membership> Memberships(FormKey key) => FormParticipants.OnForm(db, key.TenantId, key.FormId);

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
    /// The first filters by the live Property ids just read, as
    /// <see cref="PropertyValuesWriter.ActiveCells(IAudienceDbContext, ActiveCellsKey)"/> does. The second reads the Test participants' cells by their ids, with no Property predicate
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
        List<long> propertyIds = IdsOf(properties);
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
        ValuesScope scope,
        IQueryable<Membership> memberships,
        CancellationToken cancellationToken) =>
        await PresetsAsync(scope, await Named(memberships).ToListAsync(cancellationToken), cancellationToken);

    private async Task<List<PresetSource>> PresetsAsync(
        ValuesScope scope,
        List<NamedMembership> participants,
        CancellationToken cancellationToken)
    {
        if (participants.Count == 0)
        {
            return [];
        }

        Dictionary<long, Dictionary<long, string>> values = await CellsAsync(
            scope, participants.Select(row => (row.MembershipId, row.Identifier)), cancellationToken);
        return participants.ConvertAll(participant => new PresetSource(
            participant.Identifier,
            values.GetValueOrDefault(participant.MembershipId) ?? [],
            participant.IsTest));
    }

    private IQueryable<NamedMembership> Named(IQueryable<Membership> memberships) =>
        FormParticipants.Of(db, memberships).Select(participant => new NamedMembership(
            participant.Membership.Id, participant.Member.Identifier, participant.Membership.IsTest));

    /// <summary>The memberships whose member's normalized identifier is one of <paramref name="keys"/>.</summary>
    private IQueryable<KeyedMembership> Keyed(
        FormKey key,
        IQueryable<Membership> memberships,
        IReadOnlyCollection<string> keys) =>
        memberships.Join(
            db.Members.Where(member => member.TenantId == key.TenantId && keys.Contains(member.NormalizedIdentifier)),
            membership => membership.MemberId,
            member => member.Id,
            (membership, member) => new KeyedMembership(
                membership.Id, membership.IsTest, member.NormalizedIdentifier, member.Identifier));

    /// <summary>
    /// The values of the memberships just read, read as the participant panel reads them (<see cref="ParticipantValues"/>).
    /// By id, not by the membership query again: a participant removed between the two statements keeps the values they
    /// were read with, never an empty preset. At most <see cref="PresetLimit"/> ids, plus those a write-back saved.
    /// </summary>
    private Task<Dictionary<long, Dictionary<long, string>>> CellsAsync(
        ValuesScope scope,
        IEnumerable<(long MembershipId, string Identifier)> participants,
        CancellationToken cancellationToken) =>
        ParticipantValues.ReadAsync(
            db,
            new ParticipantCells(
                scope.Key.TenantId,
                scope.MatchKey,
                participants.ToDictionary(participant => participant.MembershipId, participant => participant.Identifier),
                scope.PropertyIds),
            cancellationToken);

    private sealed record NamedMembership(long MembershipId, string Identifier, bool IsTest);

    /// <summary>
    /// The form whose values are read, its match-key Property, which they show the identifier in, and its live
    /// Properties' ids, which the cells are read for.
    /// </summary>
    private sealed record ValuesScope(FormKey Key, MatchKey? MatchKey, IReadOnlyCollection<long> PropertyIds);

    private sealed record KeyedMembership(long MembershipId, bool IsTest, string Key, string Identifier);
}

/// <summary>One form of one tenant.</summary>
internal readonly record struct FormKey(long TenantId, long FormId);

/// <summary>
/// A form's Properties in tab order, the participants on it a write names by normalized identifier, each named
/// identifier's normalized form, and the form's match-key Property.
/// </summary>
internal sealed record KeyedAudience(
    IReadOnlyList<Property> Properties,
    IReadOnlyDictionary<string, OnForm> OnForm,
    IReadOnlyDictionary<string, string> Keys,
    MatchKey? MatchKey);
