using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

public sealed class SubmissionByFormIdAndSubmitterIdSpec : SingleResultSpecification<Submission>
{
    public SubmissionByFormIdAndSubmitterIdSpec(long formId, long submitterId)
    {
        FormId = formId;
        SubmitterId = submitterId;

        Query.Where(s =>
            s.FormId == formId &&
            s.SubmitterId == submitterId &&
            !s.IsTestSubmission);
    }

    public long FormId { get; }

    public long SubmitterId { get; }
}
