using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

/// <summary>
/// Specification for retrieving a form submission. Note that both Ids are required - the submission Id and the respective form Id it belongs to
/// </summary>
public class SubmissionByFormIdAndSubmissionIdSpec : SingleResultSpecification<Submission>
{
    public SubmissionByFormIdAndSubmissionIdSpec(long FormId, long SubmissionId)
    {
        Query.Where(s => s.Id == SubmissionId && s.FormDefinition.FormId == FormId);
    }
}

/// <summary>
/// Validates submission belongs to form for public/token flows when request tenant context may not match the submission's tenant.
/// </summary>
public sealed class SubmissionByFormIdAndSubmissionIdForPublicAccessSpec : SingleResultSpecification<Submission>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubmissionByFormIdAndSubmissionIdForPublicAccessSpec"/> class.
    /// </summary>
    /// <param name="formId">The ID of the form to get the submission for.</param>
    /// <param name="submissionId">The ID of the submission to get.</param>
    public SubmissionByFormIdAndSubmissionIdForPublicAccessSpec(long formId, long submissionId)
    {
        Query
            .IgnoreQueryFilters()
            .Where(s => !s.IsDeleted && s.Id == submissionId && s.FormId == formId);
    }
}
