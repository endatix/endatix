using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.People;

/// <summary>
/// Participants' values, read one way for the people list, the participant panel and Preview as: their live cells
/// (current Properties only), by property id, in one statement.
/// </summary>
internal static class ParticipantValues
{
    /// <summary>Values by membership id; a participant with none is absent.</summary>
    public static async Task<Dictionary<long, Dictionary<long, string>>> ReadAsync(
        IAudienceDbContext db,
        ParticipantCells which,
        CancellationToken cancellationToken)
    {
        var cells = await PropertyValuesWriter
            .ActiveCells(db, which.TenantId, which.MembershipIds)
            .Select(cell => new { cell.MembershipId, cell.PropertyId, cell.Value })
            .ToListAsync(cancellationToken);
        return cells
            .GroupBy(cell => cell.MembershipId)
            .ToDictionary(
                group => group.Key,
                group => group.ToDictionary(cell => cell.PropertyId, cell => cell.Value));
    }

    /// <summary>One participant's values; empty when they have none.</summary>
    public static async Task<Dictionary<long, string>> ReadOneAsync(
        IAudienceDbContext db,
        Membership membership,
        CancellationToken cancellationToken) =>
        (await ReadAsync(db, new ParticipantCells(membership.TenantId, [membership.Id]), cancellationToken))
            .GetValueOrDefault(membership.Id) ?? [];
}

/// <summary>The memberships of one tenant whose values to read.</summary>
internal sealed record ParticipantCells(long TenantId, IReadOnlyCollection<long> MembershipIds);
