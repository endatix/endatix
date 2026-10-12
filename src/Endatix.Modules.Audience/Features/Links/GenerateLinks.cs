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
        var gate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, request.TenantId, request.FormId, cancellationToken));
        if (!gate.IsSuccess)
        {
            return gate.ToErrorResult<IReadOnlyList<IssuedLinkDto>>();
        }

        var membershipIds = await MembershipIdsAsync(request.FormId, cancellationToken);
        if (membershipIds.Count > MaxLinks)
        {
            return Result.Invalid(new ValidationError("This form has more than 5,000 people."));
        }

        var existing = await ExistingAsync(request.FormId, cancellationToken);
        var issued = IssueMissing(request, membershipIds, existing);
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success<IReadOnlyList<IssuedLinkDto>>(issued);
    }

    /// <summary>Up to one more than the cap, so a form over it is refused without reading it all.</summary>
    private Task<List<long>> MembershipIdsAsync(long formId, CancellationToken cancellationToken) =>
        db.Memberships
            .Where(membership => membership.FormId == formId)
            .Select(membership => membership.Id)
            .Take(MaxLinks + 1)
            .ToListAsync(cancellationToken);

    private async Task<HashSet<long>> ExistingAsync(long formId, CancellationToken cancellationToken)
    {
        var ids = await db.Links
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
        foreach (var membershipId in membershipIds)
        {
            if (existing.Contains(membershipId))
            {
                continue;
            }

            (var link, var token) = AudienceLink.Issue(
                request.TenantId,
                request.FormId,
                membershipId);
            db.Links.Add(link);
            issued.Add(new IssuedLinkDto(membershipId, token));
        }

        return issued;
    }
}
