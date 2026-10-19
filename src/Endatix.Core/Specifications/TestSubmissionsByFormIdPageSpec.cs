using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

/// <summary>A form's live test submissions after a given id, in id order (keyset paging).</summary>
public sealed class TestSubmissionsByFormIdPageSpec : Specification<Submission>
{
    /// <summary>The next <paramref name="take"/> test submissions of <paramref name="formId"/> after <paramref name="afterId"/>.</summary>
    /// <param name="formId">The form.</param>
    /// <param name="afterId">The last id of the previous page, or 0 for the first page.</param>
    /// <param name="take">The page size.</param>
    public TestSubmissionsByFormIdPageSpec(long formId, long afterId, int take) =>
        Query.Where(submission => submission.FormId == formId && submission.IsTestSubmission && submission.Id > afterId)
            .OrderBy(submission => submission.Id)
            .Take(take);
}
