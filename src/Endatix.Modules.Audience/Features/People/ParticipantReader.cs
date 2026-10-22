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
    /// The participant on the form (<see cref="FormParticipants"/>), or <see langword="null"/> when they are not on it,
    /// were removed, or belong to another tenant.
    /// </summary>
    public async Task<FoundParticipant?> FindOnFormAsync(ParticipantKey key, CancellationToken cancellationToken)
    {
        var row = await FormParticipants.Of(db, FormParticipants.ByMember(db, key))
            .Select(participant => new
            {
                participant.Membership,
                participant.Member.Identifier,
                participant.Member.SubmitterId,
            })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null
            ? null
            : new FoundParticipant(await ReadAsync(row.Membership, row.Identifier, cancellationToken), row.SubmitterId);
    }

    public async Task<PersonDto> ReadAsync(Membership membership, string identifier, CancellationToken cancellationToken)
    {
        Dictionary<long, string> values = await ParticipantValues.ReadOneAsync(
            db, ParticipantCells.Of(membership, identifier), cancellationToken);
        IReadOnlyDictionary<long, LinkStatusDto> links = await linkStatuses.ForAsync([membership.Id], cancellationToken);
        return new PersonDto(
            membership.Id, membership.MemberId, identifier, values, membership.IsTest, links[membership.Id]);
    }
}

/// <summary>One participant on one form of one tenant, keyed by audience member id.</summary>
internal sealed record ParticipantKey(long TenantId, long FormId, long MemberId);

/// <summary>A participant found on a form, with the Submitter bound to them, if any.</summary>
internal sealed record FoundParticipant(PersonDto Participant, long? SubmitterId);
