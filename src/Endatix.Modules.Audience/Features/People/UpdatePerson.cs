using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Infrastructure.Data.Locking;
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
    internal const string Busy = "This participant is being changed. Try again.";

    /// <summary>
    /// The form, the membership and the values are checked before the lock; the write re-reads the membership under
    /// it, so an edit racing the participant's removal is not found instead of leaving a live value behind.
    /// </summary>
    public async Task<Result<PersonDto>> Handle(
        UpdatePersonCommand request,
        CancellationToken cancellationToken)
    {
        Result<Membership> loaded = await LoadAsync(request, cancellationToken);
        if (!loaded.IsSuccess)
        {
            return loaded.ToErrorResult<PersonDto>();
        }

        Result written = await WriteAsync(request, loaded.Value, cancellationToken);
        return written.IsSuccess
            ? await ToDtoAsync(loaded.Value, cancellationToken)
            : written.ToErrorResult<PersonDto>();
    }

    private async Task<Result> WriteAsync(
        UpdatePersonCommand request,
        Membership membership,
        CancellationToken cancellationToken)
    {
        try
        {
            bool written = await valuesWriter.WriteAsync(
                new PropertyValueWrite(db, request.TenantId, membership.Id, request.Values),
                cancellationToken);
            return written ? Result.Success() : Result.NotFound("Audience membership not found.");
        }
        catch (TransactionLockTimeoutException)
        {
            return Result.Unavailable(Busy);
        }
    }

    private async Task<Result<Membership>> LoadAsync(
        UpdatePersonCommand request,
        CancellationToken cancellationToken)
    {
        Result formGate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, request.TenantId, request.FormId, cancellationToken));
        if (!formGate.IsSuccess)
        {
            return formGate.ToErrorResult<Membership>();
        }

        Membership? membership = await db.Memberships.AsNoTracking().FirstOrDefaultAsync(
            row => row.Id == request.MembershipId && row.FormId == request.FormId,
            cancellationToken);
        return membership is null
            ? Result.NotFound("Audience membership not found.")
            : await ValidateValuesAsync(request, membership, cancellationToken);
    }

    private async Task<Result<Membership>> ValidateValuesAsync(
        UpdatePersonCommand request,
        Membership membership,
        CancellationToken cancellationToken)
    {
        Result values = await PropertyValuesWriter.ValidateAsync(
            new PropertyValuesCheck(db, request.FormId, request.Values),
            cancellationToken);
        return values.IsSuccess
            ? Result.Success(membership)
            : values.ToErrorResult<Membership>();
    }

    private async Task<Result<PersonDto>> ToDtoAsync(
        Membership membership,
        CancellationToken cancellationToken)
    {
        Member? member = await db.Members.AsNoTracking()
            .FirstOrDefaultAsync(row => row.Id == membership.MemberId, cancellationToken);
        if (member is null)
        {
            return Result.NotFound("Audience member not found.");
        }

        return await participantReader.ReadAsync(membership, member.Identifier, cancellationToken);
    }
}
