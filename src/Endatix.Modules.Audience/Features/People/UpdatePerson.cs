using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.People;

/// <summary>
/// Updates property values for a person on this form.
/// </summary>
public sealed record UpdatePersonCommand(
    long TenantId,
    long FormId,
    long MembershipId,
    IReadOnlyDictionary<long, string> Values) : ICommand<Result<PersonDto>>;

internal sealed class UpdatePersonHandler(
    IAudienceDbContext db,
    IRepository<Form> forms,
    LockedValuesWriter valuesWriter,
    ParticipantReader participantReader)
    : ICommandHandler<UpdatePersonCommand, Result<PersonDto>>
{
    /// <summary>
    /// The form, the membership and the values are checked before the lock; the write re-reads the membership under
    /// it, so an edit racing the participant's removal is not found instead of leaving a live value behind.
    /// </summary>
    public async Task<Result<PersonDto>> Handle(
        UpdatePersonCommand request,
        CancellationToken cancellationToken)
    {
        Result<ParticipantOnForm> loaded = await LoadAsync(request, cancellationToken);
        if (!loaded.IsSuccess)
        {
            return loaded.ToErrorResult<PersonDto>();
        }

        ParticipantOnForm participant = loaded.Value;
        ValuesEdit edit = new(
            request.TenantId, request.FormId, participant.Membership.Id,
            MatchKeyProperty.Stored(request.Values, participant.MatchKey));
        IResult? failure = await valuesWriter.WriteAsync(edit, cancellationToken);
        return failure is null
            ? await participantReader.ReadAsync(participant, cancellationToken)
            : failure.ToErrorResult<PersonDto>();
    }

    /// <summary>
    /// The participant and the form's match-key Property, then the values: a value for that Property other than the
    /// identifier is refused (<see cref="MatchKeyProperty.WriteError"/>).
    /// </summary>
    private async Task<Result<ParticipantOnForm>> LoadAsync(
        UpdatePersonCommand request,
        CancellationToken cancellationToken)
    {
        Result formGate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, request.TenantId, request.FormId, cancellationToken));
        if (!formGate.IsSuccess)
        {
            return formGate.ToErrorResult<ParticipantOnForm>();
        }

        ParticipantOnForm? participant = await FindAsync(request, cancellationToken);
        return participant is null
            ? Result.NotFound("Audience membership not found.")
            : await ValidateValuesAsync(request, participant, cancellationToken);
    }

    private async Task<ParticipantOnForm?> FindAsync(UpdatePersonCommand request, CancellationToken cancellationToken)
    {
        var row = await FormParticipants
            .Of(db, db.Memberships.AsNoTracking().Where(
                membership => membership.Id == request.MembershipId && membership.FormId == request.FormId))
            .Select(participant => new { participant.Membership, participant.Member.Identifier })
            .FirstOrDefaultAsync(cancellationToken);
        return row is null
            ? null
            : new ParticipantOnForm(
                row.Membership,
                row.Identifier,
                await MatchKeyProperty.FindAsync(db, new(request.TenantId, request.FormId), cancellationToken));
    }

    private async Task<Result<ParticipantOnForm>> ValidateValuesAsync(
        UpdatePersonCommand request,
        ParticipantOnForm participant,
        CancellationToken cancellationToken)
    {
        if (MatchKeyProperty.WriteError(request.Values, participant.MatchKey, participant.Identifier) is { } matchKeyError)
        {
            return Result.Invalid(new ValidationError(matchKeyError));
        }

        Result values = await PropertyValuesWriter.ValidateAsync(
            new PropertyValuesCheck(db, request.FormId, request.Values),
            cancellationToken);
        return values.IsSuccess
            ? Result.Success(participant)
            : values.ToErrorResult<ParticipantOnForm>();
    }
}
