using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

public sealed class SubmissionByFormIdAndSubmitterIdSpec : SingleResultSpecification<Submission>
{
    public SubmissionByFormIdAndSubmitterIdSpec(long formId, long submitterId)
    {
        Query.Where(s =>
            s.FormId == formId &&
            s.SubmitterId == submitterId &&
            !s.IsTestSubmission);
    }
}
