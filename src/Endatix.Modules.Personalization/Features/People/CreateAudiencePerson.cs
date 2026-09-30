using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Personalization.Domain;
using Endatix.Modules.Personalization.Persistence;
using Endatix.Modules.Personalization.Shared;
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
        if (request.TenantId <= 0)
        {
            return Result.Unauthorized("Tenant context is required.");
        }

        Result formResult = await FormAudienceGuard.EnsureFormExistsAsync(
            forms, request.FormId, cancellationToken);
        if (!formResult.IsSuccess)
        {
            return Result.NotFound(formResult.Errors.ToArray());
        }

        if (string.IsNullOrWhiteSpace(request.Identifier))
        {
            return Result.Invalid(new ValidationError("Identifier is required."));
        }

        string normalized = AudienceMember.Normalize(request.Identifier);

        AudienceMember? member = await db.AudienceMembers
            .FirstOrDefaultAsync(
                row => row.TenantId == request.TenantId && row.Identifier == normalized,
                cancellationToken);

        if (member is null)
        {
            member = new AudienceMember(request.TenantId, normalized);
            db.AudienceMembers.Add(member);
            await db.SaveChangesAsync(cancellationToken);
        }

        bool alreadyOnForm = await db.AudienceMemberships.AnyAsync(
            membership => membership.FormId == request.FormId
                && membership.AudienceMemberId == member.Id,
            cancellationToken);
        if (alreadyOnForm)
        {
            return Result.Conflict("This person is already on this form's audience.");
        }

        Result propertyCheck = await AudiencePropertyValuesWriter.ValidatePropertyIdsAsync(
            db, request.FormId, request.Values?.Keys, cancellationToken);
        if (!propertyCheck.IsSuccess)
        {
            return Result.Invalid(propertyCheck.ValidationErrors.ToArray());
        }

        AudienceMembership membership = new(request.TenantId, request.FormId, member.Id);
        db.AudienceMemberships.Add(membership);
        await db.SaveChangesAsync(cancellationToken);

        IReadOnlyDictionary<long, string> values = request.Values ?? new Dictionary<long, string>();
        await AudiencePropertyValuesWriter.UpsertAsync(
            db, request.TenantId, membership.Id, values, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);

        return Result.Success(new AudiencePersonDto(
            membership.Id,
            member.Id,
            member.Identifier,
            values));
    }
}
