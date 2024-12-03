using Ardalis.Specification;
using Endatix.Core.Entities;

namespace Endatix.Core.Specifications;

public class SubmissionByTokenSpec : Specification<Submission>
{
    public SubmissionByTokenSpec(string token)
    {
        Query
            .AsNoTracking()
            .Where(s => s.Token != null && s.Token.Value == token);
    }
}
