using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.People;

/// <summary>
/// Reads one participant on a form by their member id, with their submissions on that form.
/// </summary>
public sealed record GetParticipantQuery(long TenantId, long FormId, long MemberId) : IQuery<Result<ParticipantDto>>;

/// <summary>
/// A participant with the newest <see cref="ParticipantSubmissionsSpec.Limit"/> of their submissions on the form, and
/// how many they have in all.
/// </summary>
public sealed record ParticipantDto(
    PersonDto Person,
    IReadOnlyList<ParticipantSubmissionDto> Submissions,
    int SubmissionTotal);

/// <summary>
/// One of a participant's submissions. <see cref="SnapshotSource"/> is the Snapshot's source (<c>link</c>,
/// <c>on_behalf</c> or <c>test</c>), or <see langword="null"/> when the submission has no Snapshot.
/// </summary>
public sealed record ParticipantSubmissionDto(
    long Id,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    bool IsComplete,
    string Status,
    bool IsTest,
    string? SnapshotSource);

internal sealed class GetParticipantHandler(
    IAudienceDbContext db,
    IRepository<Form> forms,
    IRepository<Submission> submissions,
    LinkStatuses linkStatuses)
    : IQueryHandler<GetParticipantQuery, Result<ParticipantDto>>
{
    public async Task<Result<ParticipantDto>> Handle(GetParticipantQuery request, CancellationToken cancellationToken)
    {
        Result gate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, request.TenantId, request.FormId, cancellationToken));
        if (!gate.IsSuccess)
        {
            return gate.ToErrorResult<ParticipantDto>();
        }

        ParticipantRow? row = await FindAsync(request, cancellationToken);
        if (row is null)
        {
            return Result.NotFound("Participant not found on this form.");
        }

        PersonDto person = await PersonAsync(row, cancellationToken);
        ParticipantSubmissionsSpec spec = new(new ParticipantSubmissionsKey(
            request.FormId, row.Membership.MemberId, person.Link.SubmissionId, row.SubmitterId));
        List<ParticipantSubmissionRow> newest = await submissions.ListAsync(spec, cancellationToken);
        int total = await submissions.CountAsync(spec, cancellationToken);
        return Result.Success(new ParticipantDto(person, newest.Select(ToDto).ToList(), total));
    }

    /// <summary>
    /// The live membership on this form joined to its member. The tenant is stated as well as filtered, so SQL
    /// Server seeks the tenant-first index, as the list does.
    /// </summary>
    private Task<ParticipantRow?> FindAsync(GetParticipantQuery request, CancellationToken cancellationToken) =>
        db.Memberships.AsNoTracking()
            .Where(membership => membership.TenantId == request.TenantId
                && membership.FormId == request.FormId
                && membership.MemberId == request.MemberId)
            .Join(
                db.Members,
                membership => membership.MemberId,
                member => member.Id,
                (membership, member) => new ParticipantRow(membership, member.Identifier, member.SubmitterId))
            .FirstOrDefaultAsync(cancellationToken);

    private async Task<PersonDto> PersonAsync(ParticipantRow row, CancellationToken cancellationToken)
    {
        Membership membership = row.Membership;
        Dictionary<long, string> values = await PropertyValuesWriter
            .ActiveCells(db, membership.TenantId, [membership.Id])
            .ToDictionaryAsync(value => value.PropertyId, value => value.Value, cancellationToken);
        IReadOnlyDictionary<long, LinkStatusDto> links = await linkStatuses.ForAsync([membership.Id], cancellationToken);
        return new PersonDto(
            membership.Id, membership.MemberId, row.Identifier, values, membership.IsTest, links[membership.Id]);
    }

    private static ParticipantSubmissionDto ToDto(ParticipantSubmissionRow row) => new(
        row.Id,
        row.CreatedAt,
        row.CompletedAt,
        row.IsComplete,
        row.Status,
        row.IsTest,
        PersonalizationReader.Read(row.Snapshot, includeValues: false)?.Source);

    private sealed record ParticipantRow(Membership Membership, string Identifier, long? SubmitterId);
}
