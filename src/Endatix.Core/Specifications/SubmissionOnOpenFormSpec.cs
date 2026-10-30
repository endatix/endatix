using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

/// <summary>
/// The submission, live, on its form while the form is live and enabled: what a signed access token may open.
/// </summary>
public sealed class SubmissionOnOpenFormSpec : SingleResultSpecification<Submission>
{
    /// <summary>Both ids are required: the submission, and the form it must belong to.</summary>
    public SubmissionOnOpenFormSpec(long formId, long submissionId)
    {
        Query.Where(submission => submission.Id == submissionId
            && submission.FormDefinition.FormId == formId
            && !submission.Form.IsDeleted
            && submission.Form.IsEnabled);
    }
}
