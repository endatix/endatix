using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Specifications;

namespace Endatix.Persistence.SqlServer;

internal sealed class SubmitterProfileFilterEvaluator : IEvaluator
{
    public bool IsCriteriaEvaluator => true;

    public IQueryable<T> GetQuery<T>(IQueryable<T> query, ISpecification<T> specification) where T : class
    {
        if (typeof(T) == typeof(Submission) &&
            specification is ISubmitterProfileFilterSpecification { SubmitterProfileFilters.Count: > 0 })
        {
            throw new NotSupportedException("submitterProfile filters are PostgreSQL-only in this MVP and are not supported by the SQL Server persistence provider yet. Add computed-column indexes for configured hot keys before enabling these filters at scale.");
        }

        return query;
    }
}
