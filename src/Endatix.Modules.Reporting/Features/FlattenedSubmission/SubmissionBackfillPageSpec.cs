using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Modules.Reporting.Features.FlattenedSubmission;

/// <summary>
/// One submission in a backfill page. Stamps decide whether an existing flatten is still current.
/// Soft-deleted submissions stay out via the global query filter.
/// </summary>
internal sealed record SubmissionBackfillCandidate(
    long SubmissionId,
    DateTime? ModifiedAt,
    DateTime CreatedAt);

internal sealed class SubmissionBackfillPageSpec : Specification<Submission, SubmissionBackfillCandidate>
{
    public SubmissionBackfillPageSpec(
        long formId,
        long? afterSubmissionId,
        int take,
        SubmissionBackfillCompletion completion = SubmissionBackfillCompletion.Completed)
    {
        var wantComplete = completion == SubmissionBackfillCompletion.Completed;

        Query.Where(submission => submission.FormId == formId && submission.IsComplete == wantComplete);

        if (afterSubmissionId.HasValue)
        {
            Query.Where(submission => submission.Id > afterSubmissionId.Value);
        }

        Query
            .OrderBy(submission => submission.Id)
            .Take(take)
            .AsNoTracking()
            .Select(submission => new SubmissionBackfillCandidate(
                submission.Id,
                submission.ModifiedAt,
                submission.CreatedAt));
    }
}
