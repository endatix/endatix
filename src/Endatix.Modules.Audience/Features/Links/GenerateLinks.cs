using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Links;

public sealed record IssuedLinkDto(long MembershipId, string Token);

public sealed record GenerateLinksCommand(long TenantId, long FormId) : ICommand<Result<IReadOnlyList<IssuedLinkDto>>>;

internal sealed class GenerateLinksHandler(IAudienceDbContext db, IRepository<Form> forms)
    : ICommandHandler<GenerateLinksCommand, Result<IReadOnlyList<IssuedLinkDto>>>
{
    public const int MaxLinks = 5_000;

    public async Task<Result<IReadOnlyList<IssuedLinkDto>>> Handle(
        GenerateLinksCommand request,
        CancellationToken cancellationToken)
    {
        Form? form = await forms.GetByIdAsync(request.FormId, cancellationToken);
        if (form is null || form.TenantId != request.TenantId)
        {
            return Result.NotFound("Form not found.");
        }

        List<long> membershipIds = await db.Memberships
            .Where(membership => membership.FormId == request.FormId)
            .Select(membership => membership.Id)
            .Take(MaxLinks + 1)
            .ToListAsync(cancellationToken);
        if (membershipIds.Count > MaxLinks)
        {
            return Result.Invalid(new ValidationError("This form has more than 5,000 people."));
        }

        HashSet<long> existing = await ExistingAsync(request.FormId, cancellationToken);
        List<IssuedLinkDto> issued = IssueMissing(request, membershipIds, existing);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success<IReadOnlyList<IssuedLinkDto>>(issued);
    }

    private async Task<HashSet<long>> ExistingAsync(long formId, CancellationToken cancellationToken)
    {
        List<long> ids = await db.Links
            .Where(link => link.FormId == formId)
            .Select(link => link.MembershipId)
            .ToListAsync(cancellationToken);
        return ids.ToHashSet();
    }

    private List<IssuedLinkDto> IssueMissing(
        GenerateLinksCommand request,
        List<long> membershipIds,
        HashSet<long> existing)
    {
        List<IssuedLinkDto> issued = [];
        foreach (long membershipId in membershipIds)
        {
            if (existing.Contains(membershipId))
            {
                continue;
            }

            (AudienceLink link, string token) = AudienceLink.Issue(
                request.TenantId,
                request.FormId,
                membershipId);
            db.Links.Add(link);
            issued.Add(new IssuedLinkDto(membershipId, token));
        }

        return issued;
    }
}
