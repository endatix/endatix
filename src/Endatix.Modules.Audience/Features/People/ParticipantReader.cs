using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.People;

/// <summary>
/// One participant on a form as <see cref="PersonDto"/>: their active cells, read as the list reads them, and their
/// Link status. The list reads a whole page at once instead.
/// </summary>
internal sealed class ParticipantReader(IAudienceDbContext db, LinkStatuses linkStatuses)
{
    /// <summary>
    /// The participant's live membership on the form, or <see langword="null"/> when they are not on it, were removed,
    /// or belong to another tenant. The tenant is stated as well as filtered, so SQL Server seeks the tenant-first
    /// index, as the list does.
    /// </summary>
    public async Task<FoundParticipant?> FindOnFormAsync(ParticipantKey key, CancellationToken cancellationToken)
    {
        var row = await db.Memberships.AsNoTracking()
            .Where(membership => membership.TenantId == key.TenantId
                && membership.FormId == key.FormId
                && membership.MemberId == key.MemberId)
            .Join(
                db.Members,
                membership => membership.MemberId,
                member => member.Id,
                (membership, member) => new { Membership = membership, member.Identifier, member.SubmitterId })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null
            ? null
            : new FoundParticipant(await ReadAsync(row.Membership, row.Identifier, cancellationToken), row.SubmitterId);
    }

    public async Task<PersonDto> ReadAsync(Membership membership, string identifier, CancellationToken cancellationToken)
    {
        Dictionary<long, string> values = await PropertyValuesWriter
            .ActiveCells(db, membership.TenantId, [membership.Id])
            .ToDictionaryAsync(value => value.PropertyId, value => value.Value, cancellationToken);
        IReadOnlyDictionary<long, LinkStatusDto> links = await linkStatuses.ForAsync([membership.Id], cancellationToken);
        return new PersonDto(
            membership.Id, membership.MemberId, identifier, values, membership.IsTest, links[membership.Id]);
    }
}

/// <summary>One participant on one form of one tenant, keyed by audience member id.</summary>
internal sealed record ParticipantKey(long TenantId, long FormId, long MemberId);

/// <summary>A participant found on a form, with the Submitter bound to them, if any.</summary>
internal sealed record FoundParticipant(PersonDto Participant, long? SubmitterId);
