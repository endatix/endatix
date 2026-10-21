using Endatix.Infrastructure.Data;
using Endatix.Modules.Audience.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Modules.Audience.Features.People;

/// <summary>Where a participant's personalised link stands.</summary>
public enum LinkStatusKind
{
    /// <summary>No link: never issued, or a test participant, who is never issued one.</summary>
    None,

    /// <summary>Issued and not opened, or freed again because the submission it opened was marked as test.</summary>
    Issued,

    /// <summary>Opened a submission.</summary>
    Opened,
}

/// <summary>
/// A participant's Link status. <see cref="SubmissionId"/> is the submission the link opened while it is live; an
/// opened link whose submission was deleted keeps <see cref="LinkStatusKind.Opened"/> without it.
/// </summary>
public sealed record LinkStatusDto(LinkStatusKind Status, DateTime? OpenedAt = null, long? SubmissionId = null)
{
    /// <summary>No link.</summary>
    public static LinkStatusDto None { get; } = new(LinkStatusKind.None);

    /// <summary>A link that has not opened a submission.</summary>
    public static LinkStatusDto Issued { get; } = new(LinkStatusKind.Issued);
}

/// <summary>The link columns the status reads.</summary>
internal sealed record LinkClaim(DateTime? OpenedAt, long? SubmissionId);

/// <summary>The submission a link names, deleted or not.</summary>
internal sealed record ClaimedSubmission(long Id, bool IsDeleted, bool IsTest);

/// <summary>
/// The only Link status rule. A page of memberships costs two statements whatever its size: one read of their
/// links, then one primary-key read of the submissions those links name.
/// </summary>
internal sealed class LinkStatuses(IAudienceDbContext db, AppDbContext app)
{
    /// <summary>A status for each of <paramref name="membershipIds"/>; one without a link is <see cref="LinkStatusDto.None"/>.</summary>
    public async Task<IReadOnlyDictionary<long, LinkStatusDto>> ForAsync(
        IReadOnlyCollection<long> membershipIds,
        CancellationToken cancellationToken)
    {
        if (membershipIds.Count == 0)
        {
            return new Dictionary<long, LinkStatusDto>();
        }

        Dictionary<long, LinkClaim> links = await LinksAsync(membershipIds, cancellationToken);
        Dictionary<long, ClaimedSubmission> submissions = await ClaimedSubmissionsAsync(links.Values, cancellationToken);
        return membershipIds.Distinct().ToDictionary(
            membershipId => membershipId,
            membershipId => Of(
                links.GetValueOrDefault(membershipId),
                links.GetValueOrDefault(membershipId)?.SubmissionId is long submissionId
                    ? submissions.GetValueOrDefault(submissionId)
                    : null));
    }

    /// <summary>
    /// A claim on a submission marked as test, deleted or not, is stale: redeem clears it and opens afresh, so the
    /// link reads <see cref="LinkStatusKind.Issued"/> before the outbox gets there. A deleted real response, or one
    /// that is gone, still holds the link: redeem refuses it.
    /// </summary>
    internal static LinkStatusDto Of(LinkClaim? link, ClaimedSubmission? submission) =>
        (link, submission) switch
        {
            (null, _) => LinkStatusDto.None,
            ({ SubmissionId: null }, _) => LinkStatusDto.Issued,
            (_, { IsTest: true }) => LinkStatusDto.Issued,
            (_, { IsDeleted: false }) => new LinkStatusDto(LinkStatusKind.Opened, link.OpenedAt, submission.Id),
            _ => new LinkStatusDto(LinkStatusKind.Opened, link.OpenedAt),
        };

    private Task<Dictionary<long, LinkClaim>> LinksAsync(
        IReadOnlyCollection<long> membershipIds,
        CancellationToken cancellationToken) =>
        db.Links.AsNoTracking()
            .Where(link => membershipIds.Contains(link.MembershipId))
            .ToDictionaryAsync(
                link => link.MembershipId,
                link => new LinkClaim(link.OpenedAt, link.SubmissionId),
                cancellationToken);

    /// <summary>Only the soft-delete filter is lifted, as in redeem: a deleted test still frees its link.</summary>
    private async Task<Dictionary<long, ClaimedSubmission>> ClaimedSubmissionsAsync(
        IEnumerable<LinkClaim> links,
        CancellationToken cancellationToken)
    {
        List<long> submissionIds = links
            .Select(link => link.SubmissionId)
            .OfType<long>()
            .ToList();
        if (submissionIds.Count == 0)
        {
            return [];
        }

        return await app.Submissions.AsNoTracking()
            .IgnoreQueryFilters([EndatixQueryFilterNames.SoftDelete])
            .Where(submission => submissionIds.Contains(submission.Id))
            .Select(submission => new ClaimedSubmission(submission.Id, submission.IsDeleted, submission.IsTestSubmission))
            .ToDictionaryAsync(submission => submission.Id, cancellationToken);
    }
}
