using Ardalis.GuardClauses;
using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Modules.Reporting.Features.FlattenedSubmission;

/// <summary>
/// Whether a submission of a tenant exists at all, deleted or not, which form it is in, and whether that form is
/// deleted.
/// </summary>
internal sealed record SubmissionDeletionState(long FormId, bool IsDeleted, bool FormIsDeleted)
{
    /// <summary>
    /// Whether the submission is in <paramref name="formId"/> and it or its form was soft-deleted. Deleting a form
    /// deletes all its submissions in the same transaction, but earlier versions could leave one created during the
    /// deletion live in a deleted form, so the form is checked too.
    /// </summary>
    public bool IsDeletedIn(long formId) => FormId == formId && (IsDeleted || FormIsDeleted);
}

/// <summary>
/// Looks past the soft-delete and tenant query filters, so a flatten that cannot load a submission
/// can tell "deleted since it was queued" from "never existed here". The tenant is matched here because the read
/// ignores the tenant filter; callers must check the form.
/// </summary>
internal sealed class SubmissionDeletionStateSpec : SingleResultSpecification<Submission, SubmissionDeletionState>
{
    public SubmissionDeletionStateSpec(long tenantId, long submissionId)
    {
        Guard.Against.NegativeOrZero(tenantId);

        Query
            .Where(submission => submission.Id == submissionId && submission.TenantId == tenantId)
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Select(submission => new SubmissionDeletionState(
                submission.FormId,
                submission.IsDeleted,
                submission.Form.IsDeleted));
    }
}
