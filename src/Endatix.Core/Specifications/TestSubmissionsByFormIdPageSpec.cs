using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

/// <summary>A form's live test submissions after <paramref name="afterId"/>, in id order (keyset paging).</summary>
public sealed class TestSubmissionsByFormIdPageSpec : Specification<Submission>
{
    public TestSubmissionsByFormIdPageSpec(long formId, long afterId, int take) =>
        Query.Where(submission => submission.FormId == formId && submission.IsTestSubmission && submission.Id > afterId)
            .OrderBy(submission => submission.Id)
            .Take(take);
}
