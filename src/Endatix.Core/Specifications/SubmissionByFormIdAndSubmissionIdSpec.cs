using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

/// <summary>
/// Specification for retrieving a form submission. Note that both Ids are required - the submission Id and the respective form Id it belongs to
/// </summary>
/// <remarks>
/// A submission whose form is deleted is not found, even when the submission itself is live: older versions could
/// leave one behind when a create overlapped the form's deletion. The check is explicit rather than left to the
/// soft-delete filter on the navigation, so it still holds where a query opts out of that filter.
/// </remarks>
public class SubmissionByFormIdAndSubmissionIdSpec : SingleResultSpecification<Submission>
{
    public SubmissionByFormIdAndSubmissionIdSpec(long FormId, long SubmissionId)
    {
        Query.Where(s => s.Id == SubmissionId && s.FormDefinition.FormId == FormId && !s.Form.IsDeleted);
    }
}
