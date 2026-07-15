using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Modules.Reporting.Features.FlattenedSubmission;

/// <summary>
/// Completed submission IDs for a form, ordered for keyset backfill pagination.
/// </summary>
internal sealed class CompletedSubmissionIdsForBackfillSpec : Specification<Submission, long>
{
    public CompletedSubmissionIdsForBackfillSpec(long formId, long? afterSubmissionId, int take)
    {
        Query
            .Where(submission => submission.FormId == formId && submission.IsComplete);

        if (afterSubmissionId.HasValue)
        {
            Query.Where(submission => submission.Id > afterSubmissionId.Value);
        }

        Query
            .OrderBy(submission => submission.Id)
            .Take(take)
            .AsNoTracking()
            .Select(submission => submission.Id);
    }
}
