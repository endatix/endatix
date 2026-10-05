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
    IUniqueConstraintViolationChecker violations)
    : ICommandHandler<UpdatePersonCommand, Result<PersonDto>>
{
    public async Task<Result<PersonDto>> Handle(
        UpdatePersonCommand request,
        CancellationToken cancellationToken)
    {
        Result<Membership> loaded = await LoadAsync(request, cancellationToken);
        if (!loaded.IsSuccess)
        {
            return loaded.ToErrorResult<PersonDto>();
        }

        await UpsertWithRetryAsync(request, loaded.Value, cancellationToken);
        return await ToDtoAsync(loaded.Value, cancellationToken);
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

        Dictionary<long, string> allValues = await PropertyValuesWriter
            .ActiveCells(db, [membership.Id])
            .ToDictionaryAsync(value => value.PropertyId, value => value.Value, cancellationToken);
        return new PersonDto(membership.Id, member.Id, member.Identifier, allValues);
    }
}
