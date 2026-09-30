using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Personalization.Domain;
using Endatix.Modules.Personalization.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Personalization.Features.People;

/// <summary>
/// Adds a person to a form's audience (creates the tenant member when needed).
/// </summary>
public sealed record CreateAudiencePersonCommand(
    long TenantId,
    long FormId,
    string Identifier,
    IReadOnlyDictionary<long, string>? Values = null) : ICommand<Result<AudiencePersonDto>>;

internal sealed class CreateAudiencePersonHandler(
    IPersonalizationDbContext db,
    IRepository<Form> forms)
    : ICommandHandler<CreateAudiencePersonCommand, Result<AudiencePersonDto>>
{
    public async Task<Result<AudiencePersonDto>> Handle(
        CreateAudiencePersonCommand request,
        CancellationToken cancellationToken)
    {
        Result gate = await GateAsync(request, cancellationToken);
        if (!gate.IsSuccess)
        {
            return TenantFormGate.MapFailure<AudiencePersonDto>(gate);
        }

        return await CreateAsync(request, cancellationToken);
    }

    private async Task<Result> GateAsync(
        CreateAudiencePersonCommand request,
        CancellationToken cancellationToken)
    {
        Result formGate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, request.TenantId, request.FormId, cancellationToken));
        if (!formGate.IsSuccess)
        {
            return formGate;
        }

        return string.IsNullOrWhiteSpace(request.Identifier)
            ? Result.Invalid(new ValidationError("Identifier is required."))
            : await ValidateValuesAsync(request, cancellationToken);
    }

    private Task<Result> ValidateValuesAsync(
        CreateAudiencePersonCommand request,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<long> propertyIds =
            request.Values?.Keys.ToList() ?? (IReadOnlyCollection<long>)[];
        return AudiencePropertyValuesWriter.ValidatePropertyIdsAsync(
            new PropertyIdCheck(db, request.FormId, propertyIds),
            cancellationToken);
    }

    private async Task<Result<AudiencePersonDto>> CreateAsync(
        CreateAudiencePersonCommand request,
        CancellationToken cancellationToken)
    {
        AudienceMember member = await FindOrCreateMemberAsync(request, cancellationToken);
        Result membershipGate = await EnsureNotOnFormAsync(request.FormId, member.Id, cancellationToken);
        if (!membershipGate.IsSuccess)
        {
            return TenantFormGate.MapFailure<AudiencePersonDto>(membershipGate);
        }

        AudienceMembership membership = await AddMembershipAsync(request, member, cancellationToken);
        IReadOnlyDictionary<long, string> values = request.Values ?? new Dictionary<long, string>();
        await WriteValuesAsync(new AudienceValueWrite(db, request.TenantId, membership.Id, values), cancellationToken);
        return Result.Success(new AudiencePersonDto(
            membership.Id, member.Id, member.Identifier, values));
    }

    private async Task<Result> EnsureNotOnFormAsync(
        long formId,
        long memberId,
        CancellationToken cancellationToken)
    {
        bool alreadyOnForm = await db.AudienceMemberships.AnyAsync(
            membership => membership.FormId == formId && membership.AudienceMemberId == memberId,
            cancellationToken);
        return alreadyOnForm
            ? Result.Conflict("This person is already on this form's audience.")
            : Result.Success();
    }

    private async Task<AudienceMembership> AddMembershipAsync(
        CreateAudiencePersonCommand request,
        AudienceMember member,
        CancellationToken cancellationToken)
    {
        AudienceMembership membership = new(request.TenantId, request.FormId, member.Id);
        db.AudienceMemberships.Add(membership);
        await db.SaveChangesAsync(cancellationToken);
        return membership;
    }

    private async Task WriteValuesAsync(AudienceValueWrite write, CancellationToken cancellationToken)
    {
        await AudiencePropertyValuesWriter.UpsertAsync(write, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<AudienceMember> FindOrCreateMemberAsync(
        CreateAudiencePersonCommand request,
        CancellationToken cancellationToken)
    {
        string normalized = AudienceMember.Normalize(request.Identifier);
        AudienceMember? member = await db.AudienceMembers.FirstOrDefaultAsync(
            row => row.TenantId == request.TenantId && row.Identifier == normalized,
            cancellationToken);
        if (member is not null)
        {
            return member;
        }

        member = new AudienceMember(request.TenantId, normalized);
        db.AudienceMembers.Add(member);
        await db.SaveChangesAsync(cancellationToken);
        return member;
    }
}
