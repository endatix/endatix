using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Personalization.Domain;
using Endatix.Modules.Personalization.Features.Settings;
using Endatix.Modules.Personalization.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Personalization.Features.People;

/// <summary>
/// Adds a person to a form's audience (creates the tenant member when needed).
/// </summary>
public sealed record CreatePersonCommand(
    long TenantId,
    long FormId,
    string Identifier,
    IReadOnlyDictionary<long, string>? Values = null) : ICommand<Result<PersonDto>>;

internal sealed class CreatePersonHandler(
    IPersonalizationDbContext db,
    IRepository<Form> forms)
    : ICommandHandler<CreatePersonCommand, Result<PersonDto>>
{
    public async Task<Result<PersonDto>> Handle(
        CreatePersonCommand request,
        CancellationToken cancellationToken)
    {
        Result gate = await GateAsync(request, cancellationToken);
        if (!gate.IsSuccess)
        {
            return gate.ToErrorResult<PersonDto>();
        }

        Member member = await FindOrAddMemberAsync(request, cancellationToken);
        Result notOnForm = await EnsureNotOnFormAsync(request.FormId, member.Id, cancellationToken);
        if (!notOnForm.IsSuccess)
        {
            return notOnForm.ToErrorResult<PersonDto>();
        }

        return Result<PersonDto>.Created(await AddToFormAsync(request, member, cancellationToken));
    }

    private async Task<Result> GateAsync(
        CreatePersonCommand request,
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
        CreatePersonCommand request,
        CancellationToken cancellationToken)
    {
        IReadOnlyCollection<long> propertyIds =
            request.Values?.Keys.ToList() ?? (IReadOnlyCollection<long>)[];
        return PropertyValuesWriter.ValidatePropertyIdsAsync(
            new PropertyIdCheck(db, request.FormId, propertyIds),
            cancellationToken);
    }

    /// <summary>
    /// Saves the member, membership and value cells in one <c>SaveChangesAsync</c>, so a failure
    /// leaves no half-added person. Snowflake ids are stamped on <c>Add</c>, so the membership id
    /// is final before the save.
    /// </summary>
    private async Task<PersonDto> AddToFormAsync(
        CreatePersonCommand request,
        Member member,
        CancellationToken cancellationToken)
    {
        Membership membership = new(request.TenantId, request.FormId, member.Id);
        db.Memberships.Add(membership);

        IReadOnlyDictionary<long, string> values = request.Values ?? new Dictionary<long, string>();
        PropertyValuesWriter.AddAll(new PropertyValueWrite(db, request.TenantId, membership.Id, values));
        await db.SaveChangesAsync(cancellationToken);
        return new PersonDto(membership.Id, member.Id, member.Identifier, values);
    }

    private async Task<Result> EnsureNotOnFormAsync(
        long formId,
        long memberId,
        CancellationToken cancellationToken)
    {
        bool alreadyOnForm = await db.Memberships.AnyAsync(
            membership => membership.FormId == formId && membership.MemberId == memberId,
            cancellationToken);
        return alreadyOnForm
            ? Result.Conflict("This person is already on this form's audience.")
            : Result.Success();
    }

    private async Task<Member> FindOrAddMemberAsync(
        CreatePersonCommand request,
        CancellationToken cancellationToken)
    {
        string identifierKind = await IdentifierKindReader.GetAsync(db, request.TenantId, cancellationToken);
        string identifier = Member.Normalize(request.Identifier, identifierKind);
        Member? member = await db.Members.FirstOrDefaultAsync(
            row => row.TenantId == request.TenantId && row.Identifier == identifier,
            cancellationToken);
        if (member is not null)
        {
            return member;
        }

        member = new Member(request.TenantId, identifier, identifierKind);
        db.Members.Add(member);
        return member;
    }
}
