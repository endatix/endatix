using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.People;

/// <summary>
/// Participants' values, read one way for the people list, the participant panel and Preview as: their live cells
/// (current Properties only) by property id, with the <see cref="MatchKeyProperty"/> filled from their identifier.
/// One statement: the caller names the match-key Property, which it reads with what it reads anyway.
/// </summary>
internal static class ParticipantValues
{
    /// <summary>Values by membership id; a participant with none is absent.</summary>
    public static async Task<Dictionary<long, Dictionary<long, string>>> ReadAsync(
        IAudienceDbContext db,
        ParticipantCells which,
        CancellationToken cancellationToken)
    {
        Dictionary<long, Dictionary<long, string>> values = await CellsAsync(db, which, cancellationToken);
        if (which.MatchKey is not null)
        {
            foreach ((long membershipId, string identifier) in which.Identifiers)
            {
                values[membershipId] = MatchKeyProperty.Shown(
                    values.GetValueOrDefault(membershipId) ?? [], which.MatchKey, identifier);
            }
        }

        return values;
    }

    /// <summary>One participant's values; empty when they have none.</summary>
    public static async Task<Dictionary<long, string>> ReadOneAsync(
        IAudienceDbContext db,
        ParticipantCells which,
        CancellationToken cancellationToken) =>
        (await ReadAsync(db, which, cancellationToken)).Values.FirstOrDefault() ?? [];

    private static async Task<Dictionary<long, Dictionary<long, string>>> CellsAsync(
        IAudienceDbContext db,
        ParticipantCells which,
        CancellationToken cancellationToken)
    {
        var cells = await PropertyValuesWriter
            .ActiveCells(db, new(which.TenantId, which.Identifiers.Keys.ToList(), which.PropertyIds))
            .Select(cell => new { cell.MembershipId, cell.PropertyId, cell.Value })
            .ToListAsync(cancellationToken);
        return cells
            .GroupBy(cell => cell.MembershipId)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(cell => cell.PropertyId, cell => cell.Value));
    }
}

/// <summary>
/// The participants of one form whose values to read, by membership id, with their identifiers; the form's match-key
/// Property (<see langword="null"/> when it has none) and its live Properties' ids, both read by the caller.
/// </summary>
internal sealed record ParticipantCells(
    long TenantId,
    MatchKey? MatchKey,
    IReadOnlyDictionary<long, string> Identifiers,
    IReadOnlyCollection<long> PropertyIds)
{
    /// <summary>One participant.</summary>
    public static ParticipantCells Of(ParticipantOnForm participant, IReadOnlyCollection<long> propertyIds) =>
        new(
            participant.Membership.TenantId,
            participant.MatchKey,
            new Dictionary<long, string> { [participant.Membership.Id] = participant.Identifier },
            propertyIds);

    /// <summary>The live Properties' ids of a form. One statement.</summary>
    public static Task<List<long>> PropertyIdsAsync(
        IAudienceDbContext db,
        MatchKeyForm form,
        CancellationToken cancellationToken) =>
        db.Properties
            .Where(property => property.TenantId == form.TenantId && property.FormId == form.FormId)
            .Select(property => property.Id)
            .ToListAsync(cancellationToken);
}
