using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;


/// <summary>
/// Specification to find a submission by token.
/// </summary>
public class SubmissionByTokenSpec : SingleResultSpecification<Submission>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SubmissionByTokenSpec"/> class.
    /// </summary>
    /// <param name="token">The token to get the submission for.</param>
    public SubmissionByTokenSpec(string token)
    {
        Query
            .Where(s => s.Token != null && s.Token.Value == token)
            .AsNoTracking();
    }
}
