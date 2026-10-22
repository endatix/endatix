using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.People;

/// <summary>
/// Participants' values, read one way for the people list, the participant panel and Preview as: their live cells
/// (current Properties only) by property id, with the <see cref="MatchKeyProperty"/> filled from their identifier.
/// Two statements: the cells, then the match-key Property.
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
        long? matchKey = await MatchKeyProperty.IdAsync(db, new(which.TenantId, which.FormId), cancellationToken);
        if (matchKey is long propertyId)
        {
            foreach ((long membershipId, string identifier) in which.Identifiers)
            {
                values.TryAdd(membershipId, []);
                values[membershipId][propertyId] = identifier;
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
            .ActiveCells(db, which.TenantId, which.Identifiers.Keys.ToList())
            .Select(cell => new { cell.MembershipId, cell.PropertyId, cell.Value })
            .ToListAsync(cancellationToken);
        return cells
            .GroupBy(cell => cell.MembershipId)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(cell => cell.PropertyId, cell => cell.Value));
    }
}

/// <summary>The participants of one form whose values to read, by membership id, with their identifiers.</summary>
internal sealed record ParticipantCells(long TenantId, long FormId, IReadOnlyDictionary<long, string> Identifiers)
{
    /// <summary>One participant.</summary>
    public static ParticipantCells Of(Membership membership, string identifier) =>
        new(membership.TenantId, membership.FormId, new Dictionary<long, string> { [membership.Id] = identifier });
}
