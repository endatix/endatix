using Endatix.Core.Abstractions;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.Settings;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Endatix.Modules.Audience.Features.People;

/// <summary>
/// Adds a person to a form's audience (creates the tenant member when needed).
/// </summary>
public sealed record CreatePersonCommand(
    long TenantId,
    long FormId,
    string Identifier,
    IReadOnlyDictionary<long, string>? Values = null) : ICommand<Result<PersonDto>>;

internal sealed class CreatePersonHandler(
    IAudienceDbContext db,
    IRepository<Form> forms,
    IValueNormalizer normalizer,
    IUniqueConstraintViolationChecker violations)
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

        return await CreateWithRetryAsync(request, cancellationToken);
    }

    /// <summary>
    /// A parallel create can add the same person to the tenant first. That member is committed
    /// by the time the index refuses this one, so a second pass finds and reuses it.
    /// </summary>
    private async Task<Result<PersonDto>> CreateWithRetryAsync(
        CreatePersonCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await CreateOnFormAsync(request, cancellationToken);
        }
        catch (DbUpdateException ex) when (violations.IsViolationOf(
            ex,
            Member.UniqueConstraints.IdentifierPerTenant))
        {
            ((DbContext)db).ChangeTracker.Clear();
            return await CreateOnFormAsync(request, cancellationToken);
        }
    }

    /// <summary>
    /// Runs under the shared match-key lock, so creates on other forms are not blocked. A parallel
    /// add of the same person to this form loses on the membership index and gets a conflict.
    /// </summary>
    private async Task<Result<PersonDto>> CreateOnFormAsync(
        CreatePersonCommand request,
        CancellationToken cancellationToken)
    {
        try
        {
            return await AddLockedAsync(request, cancellationToken);
        }
        catch (DbUpdateException ex) when (violations.IsViolationOf(
            ex,
            Membership.UniqueConstraints.MemberPerForm))
        {
            return AlreadyOnForm();
        }
    }

    private async Task<Result<PersonDto>> AddLockedAsync(
        CreatePersonCommand request,
        CancellationToken cancellationToken)
    {
        await using IDbContextTransaction transaction =
            await MatchKeyLock.BeginSharedAsync(db, request.TenantId, cancellationToken);
        Result<PersonDto> created = await AddAsync(request, cancellationToken);
        if (created.IsSuccess)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return created;
    }

    /// <summary>
    /// Reads the match key once, under the lock, so a key change cannot slip in between checking
    /// the identifier and building the member.
    /// </summary>
    private async Task<Result<PersonDto>> AddAsync(
        CreatePersonCommand request,
        CancellationToken cancellationToken)
    {
        string identifierKind = await IdentifierKindReader.GetAsync(db, request.TenantId, cancellationToken);
        string? identifierError = Member.IdentifierError(request.Identifier, identifierKind);
        if (identifierError is not null)
        {
            return Result.Invalid(new ValidationError(identifierError));
        }

        Member member = await FindOrAddMemberAsync(request, identifierKind, cancellationToken);
        bool onForm = await db.Memberships.AnyAsync(
            membership => membership.FormId == request.FormId && membership.MemberId == member.Id,
            cancellationToken);
        return onForm
            ? AlreadyOnForm()
            : Result<PersonDto>.Created(await AddToFormAsync(request, member, cancellationToken));
    }

    private static Result<PersonDto> AlreadyOnForm() =>
        Result.Conflict("This person is already on this form's audience.");

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

        return await PropertyValuesWriter.ValidateAsync(
            new PropertyValuesCheck(db, request.FormId, request.Values),
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
        return new PersonDto(membership.Id, member.Id, member.Identifier, PropertyValuesWriter.Stored(values));
    }

    private async Task<Member> FindOrAddMemberAsync(
        CreatePersonCommand request,
        string identifierKind,
        CancellationToken cancellationToken)
    {
        string normalized = Member.Normalize(request.Identifier, identifierKind, normalizer);
        Member? member = await db.Members.FirstOrDefaultAsync(
            row => row.TenantId == request.TenantId && row.NormalizedIdentifier == normalized,
            cancellationToken);
        if (member is not null)
        {
            return member;
        }

        member = new Member(new MemberCreateArgs(
            request.TenantId, request.Identifier, identifierKind, normalizer));
        db.Members.Add(member);
        return member;
    }
}
