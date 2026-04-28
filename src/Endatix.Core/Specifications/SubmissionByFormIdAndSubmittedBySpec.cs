using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

public sealed class SubmissionByFormIdAndSubmittedBySpec : SingleResultSpecification<Submission>
{
    public SubmissionByFormIdAndSubmittedBySpec(long formId, string submittedBy)
    {
        Query.Where(s => s.FormId == formId && s.SubmittedBy == submittedBy);
    }
}
