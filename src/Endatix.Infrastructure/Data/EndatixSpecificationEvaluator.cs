using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;

namespace Endatix.Infrastructure.Data;

public sealed class EndatixSpecificationEvaluator(IEnumerable<IEvaluator> customEvaluators) : ISpecificationEvaluator
{
    private readonly IReadOnlyList<IEvaluator> _criteriaEvaluators = customEvaluators
        .Where(evaluator => evaluator.IsCriteriaEvaluator)
        .ToList();

    public IQueryable<TResult> GetQuery<T, TResult>(IQueryable<T> query, ISpecification<T, TResult> specification)
        where T : class
    {
        return SpecificationEvaluator.Default.GetQuery(ApplyCustomCriteria(query, specification), specification);
    }

    public IQueryable<T> GetQuery<T>(IQueryable<T> query, ISpecification<T> specification, bool evaluateCriteriaOnly = false)
        where T : class
    {
        return SpecificationEvaluator.Default.GetQuery(ApplyCustomCriteria(query, specification), specification, evaluateCriteriaOnly);
    }

    private IQueryable<T> ApplyCustomCriteria<T>(IQueryable<T> query, ISpecification<T> specification)
        where T : class
    {
        return _criteriaEvaluators.Aggregate(query, (current, evaluator) => evaluator.GetQuery(current, specification));
    }
}
