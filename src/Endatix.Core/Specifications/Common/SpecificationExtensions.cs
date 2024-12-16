using Ardalis.Specification;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.Specifications.Common;

/// <summary>
/// Extension methods to apply paging for Ardalis.Specification based queries.
/// </summary>
public static class SpecificationExtensions
{
    /// <summary>
    /// Paginate the query based of <see cref="PagingParameters"/> instance
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="query"></param>
    /// <param name="filter"></param>
    /// <returns>Paginate query to be used by the <see cref="ISpecificationBuilder{TEntity}"/></returns>
    public static ISpecificationBuilder<TEntity> Paginate<TEntity>(this ISpecificationBuilder<TEntity> query, PagingParameters filter)
    {
        if (filter == null)
        {
            return query;
        }

        if (filter.Page > 1)
        {
            query = query.Skip((filter.Page - 1) * filter.PageSize);
        }

        return query.Take(filter.PageSize);
    }

    /// <summary>
    /// Applies filtering to the query based on the provided filter parameters
    /// </summary>
    /// <typeparam name="TEntity">The type of entity being queried</typeparam>
    /// <param name="query">The specification builder instance</param>
    /// <param name="filters">The filter parameters to apply</param>
    /// <returns>The filtered specification builder</returns>
    public static ISpecificationBuilder<TEntity> Filter<TEntity>(this ISpecificationBuilder<TEntity> query, FilterParameters filters)
    {
        if (filters?.Criteria == null || !filters.Criteria.Any())
        {
            return query;
        }

        return filters.Criteria.Aggregate(query, (current, criterion) => current.Filter(criterion));
    }

    /// <summary>
    /// Applies a single filter criterion to the query
    /// </summary>
    /// <typeparam name="TEntity">The type of entity being queried</typeparam>
    /// <param name="query">The specification builder instance</param>
    /// <param name="filter">The filter criterion to apply</param>
    /// <returns>The filtered specification builder</returns>
    public static ISpecificationBuilder<TEntity> Filter<TEntity>(this ISpecificationBuilder<TEntity> query, FilterCriterion filter)
    {
        if (filter?.Values == null || !filter.Values.Any())
        {
            return query;
        }

        var lambda = SpecificationHelper.BuildFilterExpression<TEntity>(filter);
        return query.Where(lambda);
    }
}
