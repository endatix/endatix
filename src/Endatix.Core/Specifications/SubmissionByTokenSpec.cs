using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

public class SubmissionByTokenSpec : Specification<Submission>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubmissionByTokenSpec"/> class.
    /// </summary>
    /// <param name="token">The token to get the submission for.</param>
    public SubmissionByTokenSpec(string token)
    {
        Query
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(s => !s.IsDeleted && s.Token != null && s.Token.Value == token);
    }
}
