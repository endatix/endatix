using Endatix.Core.Abstractions.Submitters;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Domain;
using Endatix.Core.Specifications;
using Endatix.Modules.Audience.Domain;
using Endatix.Modules.Audience.Features.People;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.Links;

/// <summary>
/// What a first open locks with: the definition it pins, on a one-response form the person's Submitter, and the
/// label its response shows.
/// </summary>
internal sealed record LinkOpening(
    AudienceLink Link,
    long DefinitionId,
    long? SubmitterId,
    string SubmitterLabel,
    string Identifier);

/// <summary>The person behind a membership.</summary>
internal sealed record LinkPerson(long TenantId, long MembershipId, string Identifier, long MemberId, long? SubmitterId);

/// <summary>
/// The checks a first open makes before it takes the link's lock: the form is enabled and has a definition, and
/// the person is still on it. A link open never writes what they read, so they need no ordering, and a refused
/// open returns without waiting for the lock or holding a connection for it.
/// </summary>
internal sealed class LinkOpenGates(
    IAudienceDbContext db,
    IRepository<Form> forms,
    IRepository<Submitter> submitters,
    ISubmitterResolver resolver)
{
    /// <summary><see langword="null"/> when the link must not open.</summary>
    public async Task<LinkOpening?> CheckAsync(AudienceLink link, CancellationToken cancellationToken)
    {
        var form = await forms.FirstOrDefaultAsync(new OpenFormSpec(link.FormId), cancellationToken);
        if (form is not { IsEnabled: true, DefinitionId: long definitionId })
        {
            return null;
        }

        var person = await PersonAsync(link.MembershipId, cancellationToken);
        if (person is null)
        {
            return null;
        }

        var submitterId = form.LimitOnePerUser
            ? await SubmitterIdAsync(link.TenantId, person, cancellationToken)
            : null;
        var label = await LabelAsync(submitterId ?? person.SubmitterId, person.Identifier, cancellationToken);
        return new LinkOpening(link, definitionId, submitterId, label, person.Identifier);
    }

    /// <summary>
    /// The form's active definition while it takes submissions, read again under the link's lock: the form may have
    /// been disabled, deleted or given another active definition since <see cref="CheckAsync"/>.
    /// <see langword="null"/> when the link must not open.
    /// </summary>
    public Task<long?> OpenDefinitionIdAsync(AudienceLink link, CancellationToken cancellationToken) =>
        forms.FirstOrDefaultAsync(
            new FormSpecifications.OpenFormActiveDefinitionId(link.FormId, link.TenantId),
            cancellationToken);

    /// <summary>The person, or <see langword="null"/> once they are removed from the form.</summary>
    public Task<LinkPerson?> PersonAsync(long membershipId, CancellationToken cancellationToken) =>
        PersonAsync(db.Memberships.Where(membership => membership.Id == membershipId), cancellationToken);

    /// <summary>
    /// The person behind the first of <paramref name="memberships"/>, or <see langword="null"/> once they are removed
    /// from the form. Test submissions open through it too, so "still on the form" means one thing.
    /// </summary>
    public Task<LinkPerson?> PersonAsync(IQueryable<Membership> memberships, CancellationToken cancellationToken) =>
        FormParticipants.Of(db, memberships)
            .Select(participant => new LinkPerson(
                participant.Membership.TenantId,
                participant.Membership.Id,
                participant.Member.Identifier,
                participant.Member.Id,
                participant.Member.SubmitterId))
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// The Submitter bound to the person, else the one an on-behalf create resolves for their identifier
    /// (<c>Integration</c> + identifier), created when missing and bound once. Sharing it puts the link's response
    /// and an on-behalf one under the same <c>UX_Submissions_RestrictionKey</c>.
    /// </summary>
    private async Task<long?> SubmitterIdAsync(long tenantId, LinkPerson person, CancellationToken cancellationToken)
    {
        if (person.SubmitterId is long bound)
        {
            return bound;
        }

        var input = new SubmitterInput(person.Identifier);
        if (await ResolveAsync(new SubmitterResolveContext(tenantId, Submitter: input), cancellationToken)
            is not long submitterId)
        {
            return null;
        }

        await db.BindSubmitterOnceAsync(new SubmitterBinding(person.MemberId, submitterId), cancellationToken);
        return await BoundSubmitterIdAsync(person.MemberId, cancellationToken);
    }

    /// <summary>
    /// Finds before it creates: <see cref="ISubmitterResolver.EnsureSubmitterAsync"/> also refreshes an existing
    /// Submitter's display id and profile, and a link has neither to give.
    /// </summary>
    private async Task<long?> ResolveAsync(SubmitterResolveContext context, CancellationToken cancellationToken)
    {
        var existing = await resolver.FindExistingAsync(context, cancellationToken);
        return existing.SubmitterId
            ?? (await resolver.EnsureSubmitterAsync(context, cancellationToken)).SubmitterId;
    }

    /// <summary>The label of the person's Submitter, as an on-behalf create shows it, else their identifier.</summary>
    private async Task<string> LabelAsync(long? submitterId, string identifier, CancellationToken cancellationToken) =>
        submitterId is long id
            ? await submitters.FirstOrDefaultAsync(new SubmitterLabelSpec(id), cancellationToken) ?? identifier
            : identifier;

    /// <summary>A parallel on-behalf create may have bound its Submitter first; the bound one is the person's.</summary>
    private Task<long?> BoundSubmitterIdAsync(long memberId, CancellationToken cancellationToken) =>
        db.Members.AsNoTracking()
            .Where(member => member.Id == memberId)
            .Select(member => member.SubmitterId)
            .FirstOrDefaultAsync(cancellationToken);
}
