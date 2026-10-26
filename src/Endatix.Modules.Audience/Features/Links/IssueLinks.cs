using System.Globalization;
using Endatix.Core.Abstractions.Data;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.People;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Links;

/// <summary>The real people on a form a <see cref="PeopleFilter"/> keeps: who a link batch is for.</summary>
public sealed record LinkBatchScope(long TenantId, long FormId, PeopleFilter Filter);

/// <summary>How many real people a filter keeps, and how many of them a batch would send a link.</summary>
public sealed record PreviewLinksQuery(LinkBatchScope Scope) : IQuery<Result<LinkPreviewDto>>;

/// <param name="Matching">Real people the filter keeps.</param>
/// <param name="WithoutLink">Of those, the people with no link yet: the ones a batch issues to.</param>
/// <param name="Cap">The most links one batch issues.</param>
public sealed record LinkPreviewDto(int Matching, int WithoutLink, int Cap);

/// <summary>
/// Issues a link to every real person the filter keeps who has none, at most <see cref="LinkBatch.MAX_LINKS"/> in one call.
/// </summary>
public sealed record IssueLinksCommand(LinkBatchScope Scope) : ICommand<Result<IssuedLinksDto>>;

/// <param name="Matching">Real people the filter keeps, with or without a link.</param>
/// <param name="Issued">The links issued by this call; a person who already had one is left out.</param>
public sealed record IssuedLinksDto(int Matching, IReadOnlyList<IssuedPersonLinkDto> Issued);

public sealed record IssuedPersonLinkDto(long MembershipId, string Identifier, string Token);

/// <summary>
/// Links for the real people a filter keeps, since a test person is never sent a link. Reads memberships, members
/// and links only, never cells.
/// </summary>
internal sealed class LinkBatch(
    IAudienceDbContext db,
    PeopleQuery people,
    IUniqueConstraintViolationChecker violations)
{
    /// <summary>Counts the links a batch would issue, not the people on the form.</summary>
    public const int MAX_LINKS = 5_000;

    public Task<int> MatchingAsync(LinkBatchScope scope, CancellationToken cancellationToken) =>
        Recipients(scope).CountAsync(cancellationToken);

    public Task<int> WithoutLinkAsync(LinkBatchScope scope, CancellationToken cancellationToken) =>
        WithoutLink(scope).CountAsync(cancellationToken);

    public Task<Result<IssuedLinksDto>> IssueAsync(LinkBatchScope scope, CancellationToken cancellationToken) =>
        LinkIssueRace.RetryOnceAsync(db, violations, () => IssueOnceAsync(scope, cancellationToken));

    private IQueryable<Membership> Recipients(LinkBatchScope scope) =>
        people.Matching(scope.TenantId, scope.FormId, scope.Filter).Where(membership => !membership.IsTest);

    /// <summary>The recipients with no live link, an anti-join on the membership index of links.</summary>
    private IQueryable<Membership> WithoutLink(LinkBatchScope scope) =>
        Recipients(scope).Where(membership => !db.Links.Any(link =>
            link.TenantId == scope.TenantId && link.FormId == scope.FormId && link.MembershipId == membership.Id));

    private async Task<Result<IssuedLinksDto>> IssueOnceAsync(LinkBatchScope scope, CancellationToken cancellationToken)
    {
        var recipients = await RecipientsWithoutLinkAsync(scope, cancellationToken);
        if (recipients.Count > MAX_LINKS)
        {
            return OverCap(await WithoutLinkAsync(scope, cancellationToken));
        }

        var issued = recipients.Select(recipient => Issue(scope, recipient)).ToList();
        await db.SaveChangesAsync(cancellationToken);
        return Result.Success(new IssuedLinksDto(await MatchingAsync(scope, cancellationToken), issued));
    }

    /// <summary>Up to one more than the cap, so a batch over it is refused without reading it all.</summary>
    private Task<List<Recipient>> RecipientsWithoutLinkAsync(LinkBatchScope scope, CancellationToken cancellationToken) =>
        FormParticipants.Of(db, WithoutLink(scope))
            .OrderBy(participant => participant.Membership.Id)
            .Select(participant => new Recipient(participant.Membership.Id, participant.Member.Identifier))
            .Take(MAX_LINKS + 1)
            .ToListAsync(cancellationToken);

    private IssuedPersonLinkDto Issue(LinkBatchScope scope, Recipient recipient)
    {
        (var link, var token) = AudienceLink.Issue(scope.TenantId, scope.FormId, recipient.MembershipId);
        db.Links.Add(link);
        return new IssuedPersonLinkDto(recipient.MembershipId, recipient.Identifier, token);
    }

    private static Result<IssuedLinksDto> OverCap(int withoutLink) =>
        Result.Invalid(new ValidationError(string.Format(
            CultureInfo.InvariantCulture,
            "{0:N0} people would get a link; the limit is {1:N0} per batch. Narrow the filter.",
            withoutLink,
            MAX_LINKS)));

    private sealed record Recipient(long MembershipId, string Identifier);
}

internal sealed class PreviewLinksHandler(IRepository<Form> forms, LinkBatch batch)
    : IQueryHandler<PreviewLinksQuery, Result<LinkPreviewDto>>
{
    public async Task<Result<LinkPreviewDto>> Handle(PreviewLinksQuery request, CancellationToken cancellationToken)
    {
        var scope = request.Scope;
        var gate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, scope.TenantId, scope.FormId, cancellationToken));
        if (!gate.IsSuccess)
        {
            return gate.ToErrorResult<LinkPreviewDto>();
        }

        return Result.Success(new LinkPreviewDto(
            await batch.MatchingAsync(scope, cancellationToken),
            await batch.WithoutLinkAsync(scope, cancellationToken),
            LinkBatch.MAX_LINKS));
    }
}

internal sealed class IssueLinksHandler(IRepository<Form> forms, LinkBatch batch)
    : ICommandHandler<IssueLinksCommand, Result<IssuedLinksDto>>
{
    public async Task<Result<IssuedLinksDto>> Handle(IssueLinksCommand request, CancellationToken cancellationToken)
    {
        var scope = request.Scope;
        var gate = await TenantFormGate.EnsureAsync(
            new FormGateRequest(forms, scope.TenantId, scope.FormId, cancellationToken));
        if (!gate.IsSuccess)
        {
            return gate.ToErrorResult<IssuedLinksDto>();
        }

        return await batch.IssueAsync(scope, cancellationToken);
    }
}
