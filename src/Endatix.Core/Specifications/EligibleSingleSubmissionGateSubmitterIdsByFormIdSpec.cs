using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

public sealed class EligibleSingleSubmissionGateSubmitterIdsByFormIdSpec : Specification<Submission, string>
{
    public EligibleSingleSubmissionGateSubmitterIdsByFormIdSpec(long formId)
    {
        Query.Where(s =>
            s.FormId == formId &&
            s.SubmittedBy != null &&
            s.SubmittedBy != string.Empty &&
            !s.IsTestSubmission)
            .Select(s => s.SubmittedBy!);
    }
}
