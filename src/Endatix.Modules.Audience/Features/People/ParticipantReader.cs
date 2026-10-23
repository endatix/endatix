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

    /// <summary>As the panel reads them: the match-key Property, the cells, the Link status.</summary>
    private async Task<PersonDto> ReadAsync(Membership membership, string identifier, CancellationToken cancellationToken) =>
        await ReadAsync(
            new ParticipantOnForm(
                membership,
                identifier,
                await MatchKeyProperty.FindAsync(db, new(membership.TenantId, membership.FormId), cancellationToken)),
            cancellationToken);

    /// <summary>After a write that has read the form's match-key Property already.</summary>
    public async Task<PersonDto> ReadAsync(ParticipantOnForm participant, CancellationToken cancellationToken)
    {
        Membership membership = participant.Membership;
        Dictionary<long, string> values = await ParticipantValues.ReadOneAsync(
            db, ParticipantCells.Of(membership, participant.MatchKey, participant.Identifier), cancellationToken);
        IReadOnlyDictionary<long, LinkStatusDto> links = await linkStatuses.ForAsync([membership.Id], cancellationToken);
        return new PersonDto(
            membership.Id, membership.MemberId, participant.Identifier, values, membership.IsTest, links[membership.Id]);
    }
}

/// <summary>A participant's membership on a form, their identifier, and the form's match-key Property.</summary>
internal sealed record ParticipantOnForm(Membership Membership, string Identifier, MatchKey? MatchKey);

/// <summary>One participant on one form of one tenant, keyed by audience member id.</summary>
internal sealed record ParticipantKey(long TenantId, long FormId, long MemberId);

/// <summary>A participant found on a form, with the Submitter bound to them, if any.</summary>
internal sealed record FoundParticipant(PersonDto Participant, long? SubmitterId);
