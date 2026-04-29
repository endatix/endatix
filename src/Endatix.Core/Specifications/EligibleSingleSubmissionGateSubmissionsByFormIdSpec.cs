using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

public sealed class EligibleSingleSubmissionGateSubmissionsByFormIdSpec : Specification<Submission>
{
    public EligibleSingleSubmissionGateSubmissionsByFormIdSpec(long formId)
    {
        Query.Where(s =>
            s.FormId == formId &&
            s.SubmittedBy != null &&
            !s.IsTestSubmission);
    }
}
