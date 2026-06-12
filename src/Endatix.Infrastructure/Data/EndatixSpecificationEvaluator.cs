using Ardalis.Specification;
using Ardalis.Specification.EntityFrameworkCore;

namespace Endatix.Infrastructure.Data;

/// <summary>
/// Evaluates specifications for the Endatix platform.
/// </summary>
/// <param name="customEvaluators">The custom evaluators to use.</param>
public sealed class EndatixSpecificationEvaluator(IEnumerable<IEvaluator> customEvaluators) : ISpecificationEvaluator
{
    private readonly IReadOnlyList<IEvaluator> _criteriaEvaluators = customEvaluators
        .Where(evaluator => evaluator.IsCriteriaEvaluator)
        .ToList();

    public IQueryable<TResult> GetQuery<T, TResult>(IQueryable<T> inputQuery, ISpecification<T, TResult> specification)
        where T : class
    {
        return SpecificationEvaluator.Default.GetQuery(ApplyCustomCriteria(inputQuery, specification), specification);
    }

    public IQueryable<T> GetQuery<T>(IQueryable<T> inputQuery, ISpecification<T> specification, bool evaluateCriteriaOnly = false)
        where T : class
    {
        return SpecificationEvaluator.Default.GetQuery(ApplyCustomCriteria(inputQuery, specification), specification, evaluateCriteriaOnly);
    }

    private IQueryable<T> ApplyCustomCriteria<T>(IQueryable<T> inputQuery, ISpecification<T> specification)
        where T : class
    {
        return _criteriaEvaluators.Aggregate(inputQuery, (current, evaluator) => evaluator.GetQuery(current, specification));
    }
}
