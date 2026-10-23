using Endatix.Core.Abstractions.Data;
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
    IUniqueConstraintViolationChecker violations,
    ParticipantReader participantReader)
    : ICommandHandler<UpdatePersonCommand, Result<PersonDto>>
{
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
        await UpsertWithRetryAsync(
            request with { Values = MatchKeyProperty.Stored(request.Values, participant.MatchKey) },
            participant.Membership,
            cancellationToken);
        return await participantReader.ReadAsync(participant, cancellationToken);
    }

    /// <summary>
    /// A parallel update can add the same cell first. It is committed by the time the index
    /// refuses this one, so a second pass finds it and overwrites it instead of inserting.
    /// </summary>
    private async Task UpsertWithRetryAsync(
        UpdatePersonCommand request,
        Membership membership,
        CancellationToken cancellationToken)
    {
        try
        {
            await UpsertAsync(request, membership, cancellationToken);
        }
        catch (DbUpdateException ex) when (violations.IsViolationOf(
            ex,
            PropertyValue.UniqueConstraints.CellPerMembership))
        {
            ((DbContext)db).ChangeTracker.Clear();
            await UpsertAsync(request, membership, cancellationToken);
        }
    }

    private async Task UpsertAsync(
        UpdatePersonCommand request,
        Membership membership,
        CancellationToken cancellationToken)
    {
        await PropertyValuesWriter.UpsertAsync(
            new PropertyValueWrite(db, request.TenantId, membership.Id, request.Values),
            cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
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
