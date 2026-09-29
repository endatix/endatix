using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Modules.Reporting.Features.FlattenedSubmission;

/// <summary>
/// Whether a submission exists at all, deleted or not, and whose it is.
/// </summary>
internal sealed record SubmissionDeletionState(long TenantId, long FormId, bool IsDeleted);

/// <summary>
/// Looks past the soft-delete and tenant query filters, so a flatten that cannot load a submission
/// can tell "deleted since it was queued" from "never existed here". Callers must check tenant and form.
/// </summary>
internal sealed class SubmissionDeletionStateSpec : SingleResultSpecification<Submission, SubmissionDeletionState>
{
    public SubmissionDeletionStateSpec(long submissionId)
    {
        Query
            .Where(submission => submission.Id == submissionId)
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(submission => new SubmissionDeletionState(
                submission.TenantId,
                submission.FormId,
                submission.IsDeleted));
    }
}
