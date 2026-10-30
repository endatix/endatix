using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Core.UseCases.Submissions;

namespace Endatix.Modules.Audience.Features.People;

/// <summary>
/// Reads one participant on a form by audience member id, with their submissions on that form.
/// </summary>
public sealed record GetParticipantQuery(long TenantId, long FormId, long MemberId) : IQuery<Result<ParticipantDto>>;

/// <summary>
/// A participant with the newest <see cref="ParticipantSubmissionsSpec.Limit"/> of their submissions on the form, and
/// how many they have in all.
/// </summary>
public sealed record ParticipantDto(
    PersonDto Participant,
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
    IRepository<Form> forms,
    IRepository<Submission> submissions,
    ParticipantReader participantReader)
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

        FoundParticipant? found = await participantReader.FindOnFormAsync(
            new ParticipantKey(request.TenantId, request.FormId, request.MemberId),
            cancellationToken);
        return found is null
            ? Result.NotFound("Participant not found on this form.")
            : Result.Success(await WithSubmissionsAsync(request.FormId, found, cancellationToken));
    }

    /// <summary>The newest submissions and the total, in two statements on the same spec.</summary>
    private async Task<ParticipantDto> WithSubmissionsAsync(
        long formId,
        FoundParticipant found,
        CancellationToken cancellationToken)
    {
        PersonDto participant = found.Participant;
        ParticipantSubmissionsSpec spec = new(new ParticipantSubmissionsKey(
            formId, participant.MemberId, participant.Link.SubmissionId, found.SubmitterId));
        List<ParticipantSubmissionRow> newest = await submissions.ListAsync(spec, cancellationToken);
        int total = await submissions.CountAsync(spec, cancellationToken);
        return new ParticipantDto(participant, newest.Select(ToDto).ToList(), total);
    }

    private static ParticipantSubmissionDto ToDto(ParticipantSubmissionRow row) => new(
        row.Id,
        row.CreatedAt,
        row.CompletedAt,
        row.IsComplete,
        row.Status,
        row.IsTest,
        PersonalizationReader.Read(row.Snapshot, includeValues: false)?.Source);
}
