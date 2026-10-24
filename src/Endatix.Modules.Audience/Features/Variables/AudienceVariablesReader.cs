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
    /// Up to six statements: the Properties, participants by test flag, filled cells by Property and test flag, the
    /// match-key Property, then the first participants and their cells (<see cref="ParticipantValues"/>). Stops after
    /// the second when the form has no Properties or no participants.
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
            await FirstParticipantsAsync(ValuesScope.Of(key, matchKey, properties), savedKeys, cancellationToken),
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
        List<PresetSource> participant =
            await PresetsAsync(ValuesScope.Of(key, matchKey, properties), named, cancellationToken);
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
        ValuesScope scope = ValuesScope.Of(key, MatchKeyProperty.In(properties, identifierKind), properties);
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

    private async Task<ParticipantCounts> CountParticipantsAsync(FormKey key, CancellationToken cancellationToken)
    {
        Dictionary<bool, int> counts = await Memberships(key)
            .GroupBy(membership => membership.IsTest)
            .Select(group => new { IsTest = group.Key, Count = group.Count() })
            .ToDictionaryAsync(group => group.IsTest, group => group.Count, cancellationToken);
        return new ParticipantCounts(counts.GetValueOrDefault(true), counts.GetValueOrDefault(false));
    }

    /// <summary>
    /// Filled cells per live Property and test flag, in one GROUP BY over <c>IX_PropertyValues_Property</c> (tenant,
    /// Property, test flag): one index range per Property, with no join, no membership read and no id list. Each cell
    /// carries its membership's test flag (<see cref="PropertyValue.IsTest"/>), so nothing ties the two tables at read
    /// time. Two counts this replaced, all cells and then the Test participants' cells by their ids, read every cell
    /// twice; the join before them let PostgreSQL loop memberships over cells and time out at 30 s (#1363, #1376).
    /// Filtering by the live Property ids just read drops a deleted Property's cells. The statements share no
    /// snapshot, so a person added or removed between them could push a count past the participants; each count is
    /// clamped to <c>0..participants</c>.
    /// </summary>
    private async Task<IReadOnlyDictionary<(long PropertyId, bool IsTest), int>> CountFilledAsync(
        FormKey key,
        List<Property> properties,
        ParticipantCounts participants,
        CancellationToken cancellationToken)
    {
        List<long> propertyIds = IdsOf(properties);
        Dictionary<(long PropertyId, bool IsTest), int> cells = await db.PropertyValues.AsNoTracking()
            .Where(cell => cell.TenantId == key.TenantId && propertyIds.Contains(cell.PropertyId))
            .GroupBy(cell => new { cell.PropertyId, cell.IsTest })
            .Select(group => new { group.Key.PropertyId, group.Key.IsTest, Count = group.Count() })
            .ToDictionaryAsync(group => (group.PropertyId, group.IsTest), group => group.Count, cancellationToken);
        return propertyIds
            .SelectMany(id => new[]
            {
                ((id, true), Math.Clamp(cells.GetValueOrDefault((id, true)), 0, participants.Test)),
                ((id, false), Math.Clamp(cells.GetValueOrDefault((id, false)), 0, participants.Real)),
            })
            .ToDictionary(entry => entry.Item1, entry => entry.Item2);
    }

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
    private sealed record ValuesScope(FormKey Key, MatchKey? MatchKey, IReadOnlyCollection<long> PropertyIds)
    {
        public static ValuesScope Of(FormKey key, MatchKey? matchKey, List<Property> properties) =>
            new(key, matchKey, IdsOf(properties));
    }

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
