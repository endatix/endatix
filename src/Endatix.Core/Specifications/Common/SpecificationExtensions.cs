using System.Linq.Expressions;
using Ardalis.Specification;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Paging;
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

    /// <summary>
    /// Orders by <paramref name="keySelector"/> then by <c>Id</c> for stable paging.
    /// </summary>
    public static IOrderedSpecificationBuilder<TEntity> OrderByWithIdTiebreaker<TEntity>(
        this ISpecificationBuilder<TEntity> query,
        Expression<Func<TEntity, object?>> keySelector,
        bool descending)
        where TEntity : BaseEntity
    {
        if (descending)
        {
            return query.OrderByDescending(keySelector).ThenBy(x => x.Id);
        }

        return query.OrderBy(keySelector).ThenBy(x => x.Id);
    }

    /// <summary>
    /// Applies inclusive lower / exclusive upper bounds from <paramref name="range"/> to a non-nullable UTC timestamp.
    /// When the exclusive upper bound is <see cref="DateTime.MaxValue"/> (calendar clamp), comparison is inclusive.
    /// </summary>
    public static ISpecificationBuilder<TEntity> WhereUtcRange<TEntity>(
        this ISpecificationBuilder<TEntity> query,
        Expression<Func<TEntity, DateTime>> keySelector,
        UtcDateTimeRange range)
    {
        if (!range.HasBounds)
        {
            return query;
        }

        if (range.InclusiveFrom.HasValue)
        {
            query = query.Where(UtcDateTimeRangeExpressions.CompareDateTime(
                keySelector,
                ExpressionType.GreaterThanOrEqual,
                range.InclusiveFrom.Value));
        }

        if (range.ExclusiveTo.HasValue)
        {
            var to = range.ExclusiveTo.Value;
            query = query.Where(UtcDateTimeRangeExpressions.CompareDateTime(
                keySelector,
                UtcDateTimeRangeExpressions.ExclusiveToComparison(to),
                to));
        }

        return query;
    }

    /// <summary>
    /// Applies inclusive lower / exclusive upper bounds from <paramref name="range"/> to a nullable UTC timestamp.
    /// Null column values are excluded when either bound is set.
    /// When the exclusive upper bound is <see cref="DateTime.MaxValue"/> (calendar clamp), comparison is inclusive.
    /// </summary>
    public static ISpecificationBuilder<TEntity> WhereUtcRange<TEntity>(
        this ISpecificationBuilder<TEntity> query,
        Expression<Func<TEntity, DateTime?>> keySelector,
        UtcDateTimeRange range)
    {
        if (!range.HasBounds)
        {
            return query;
        }

        if (range.InclusiveFrom.HasValue)
        {
            query = query.Where(UtcDateTimeRangeExpressions.CompareNullableDateTime(
                keySelector,
                ExpressionType.GreaterThanOrEqual,
                range.InclusiveFrom.Value));
        }

        if (range.ExclusiveTo.HasValue)
        {
            var to = range.ExclusiveTo.Value;
            query = query.Where(UtcDateTimeRangeExpressions.CompareNullableDateTime(
                keySelector,
                UtcDateTimeRangeExpressions.ExclusiveToComparison(to),
                to));
        }

        return query;
    }
}
