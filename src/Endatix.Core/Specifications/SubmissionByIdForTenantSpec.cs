using Ardalis.GuardClauses;
using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

/// <summary>
/// One submission of a form, only when it belongs to the given tenant. Tracked, for a write. An HTTP write states the
/// tenant because a request without a usable <c>tid</c> claim leaves the ambient tenant at 0, which the tenant filter
/// reads as "every tenant".
/// </summary>
public sealed class SubmissionByIdForTenantSpec : SingleResultSpecification<Submission>
{
    /// <summary>Submission <paramref name="submissionId"/> on <paramref name="formId"/> of <paramref name="tenantId"/>.</summary>
    /// <param name="tenantId">The caller's tenant; must be positive.</param>
    /// <param name="formId">The form the submission belongs to.</param>
    /// <param name="submissionId">The submission.</param>
    public SubmissionByIdForTenantSpec(long tenantId, long formId, long submissionId)
    {
        Guard.Against.NegativeOrZero(tenantId);

        Query.Where(submission => submission.Id == submissionId
            && submission.FormId == formId
            && submission.TenantId == tenantId);
    }
}
