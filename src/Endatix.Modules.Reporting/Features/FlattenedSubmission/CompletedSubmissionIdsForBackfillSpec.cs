using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Modules.Reporting.Features.FlattenedSubmission;

/// <summary>
/// One submission id in a backfill page, with the stamps used to skip an unchanged flatten.
/// </summary>
public sealed record SubmissionBackfillCandidate(
    long SubmissionId,
    DateTime? ModifiedAt,
    DateTime CreatedAt);

/// <summary>
/// Submission ids for a form, ordered for keyset backfill pagination.
/// Soft-deleted rows stay out via the repository query filter.
/// </summary>
internal sealed class CompletedSubmissionIdsForBackfillSpec : Specification<Submission, SubmissionBackfillCandidate>
{
    public CompletedSubmissionIdsForBackfillSpec(
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
