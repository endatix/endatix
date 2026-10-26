using System.Linq.Expressions;
using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Modules.Audience.Features.Links;
using static Endatix.Modules.Audience.Features.Links.SnapshotExpression;

namespace Endatix.Modules.Audience.Features.People;

/// <summary>What the participant read keeps of one submission; the Snapshot is read for its source only.</summary>
internal sealed record ParticipantSubmissionRow(
    long Id,
    DateTime CreatedAt,
    DateTime? CompletedAt,
    bool IsComplete,
    string Status,
    bool IsTest,
    string? Snapshot);

/// <summary>Whose submissions to find: the submission their live link opened, and their bound Submitter.</summary>
internal sealed record ParticipantSubmissionsKey(long FormId, long MemberId, long? LinkSubmissionId, long? SubmitterId);

/// <summary>
/// A participant's submissions on one form, newest first: the one their link opened, their Submitter's, and
/// submissions whose Snapshot names them and are either a test submission or an on-behalf create (the bind after an
/// on-behalf save is best effort, so the Snapshot is the only join until it succeeds). One WHERE that
/// <c>IX_Submissions_FormId</c> narrows; deleted submissions drop by the query filter. A count of the same spec
/// ignores <see cref="Limit"/>.
/// </summary>
internal sealed class ParticipantSubmissionsSpec : Specification<Submission, ParticipantSubmissionRow>
{
    /// <summary>How many submissions the read returns.</summary>
    public const int Limit = 50;

    public ParticipantSubmissionsSpec(ParticipantSubmissionsKey key)
    {
        Query.Where(Theirs(key))
            .OrderByDescending(submission => submission.CreatedAt)
            .ThenByDescending(submission => submission.Id)
            .Take(Limit)
            .AsNoTracking();
        Query.Select(submission => new ParticipantSubmissionRow(
            submission.Id,
            submission.CreatedAt,
            submission.CompletedAt,
            submission.IsComplete,
            submission.Status.Code,
            submission.IsTestSubmission,
            submission.PersonalizationSnapshot));
    }

    /// <summary>
    /// On the form, and the link's submission, the Submitter's, or a Snapshot that names the participant and is a
    /// test submission or an on-behalf create. The matches from
    /// <see cref="PersonalizationSnapshotJson.NamesParticipant"/> and
    /// <see cref="PersonalizationSnapshotJson.IsOnBehalf"/> are bound to the Snapshot column here.
    /// </summary>
    private static Expression<Func<Submission, bool>> Theirs(ParticipantSubmissionsKey key)
    {
        Expression<Func<Submission, bool>> onForm = submission => submission.FormId == key.FormId;
        Expression<Func<Submission, bool>> linkOrSubmitter = submission =>
            submission.Id == key.LinkSubmissionId
            || (key.SubmitterId != null && submission.SubmitterId == key.SubmitterId);
        Expression<Func<Submission, bool>> isTest = submission => submission.IsTestSubmission;
        Expression<Func<string?, bool>> isOnBehalf = PersonalizationSnapshotJson.IsOnBehalf();
        Expression<Func<string?, bool>> namesParticipant = PersonalizationSnapshotJson.NamesParticipant(key.MemberId);

        ParameterExpression submission = Expression.Parameter(typeof(Submission), "submission");
        Expression snapshot = Expression.Property(submission, nameof(Submission.PersonalizationSnapshot));
        Expression body = Expression.AndAlso(
            BodyOf(onForm, submission),
            Expression.OrElse(
                BodyOf(linkOrSubmitter, submission),
                Expression.AndAlso(
                    Expression.OrElse(BodyOf(isTest, submission), BodyOf(isOnBehalf, snapshot)),
                    BodyOf(namesParticipant, snapshot))));
        return Expression.Lambda<Func<Submission, bool>>(body, submission);
    }
}
