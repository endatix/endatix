using System.Linq.Expressions;
using Ardalis.Specification;
using Endatix.Core.Entities;
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
    /// Applies inclusive CreatedAt lower bound and exclusive upper bound when present.
    /// When the exclusive upper bound is <see cref="DateTime.MaxValue"/> (calendar clamp),
    /// comparison is inclusive so a timestamp at the sentinel is not dropped.
    /// </summary>
    public static ISpecificationBuilder<TEntity> WhereCreatedRange<TEntity>(
        this ISpecificationBuilder<TEntity> query,
        DateTime? createdFrom,
        DateTime? createdToExclusive)
        where TEntity : BaseEntity
    {
        if (createdFrom.HasValue)
        {
            var from = createdFrom.Value;
            query = query.Where(x => x.CreatedAt >= from);
        }

        if (createdToExclusive.HasValue)
        {
            var to = createdToExclusive.Value;
            if (to == DateTime.MaxValue)
            {
                query = query.Where(x => x.CreatedAt <= to);
            }
            else
            {
                query = query.Where(x => x.CreatedAt < to);
            }
        }

        return query;
    }

    /// <summary>
    /// Applies inclusive ModifiedAt lower bound and exclusive upper bound when present.
    /// Null ModifiedAt rows are excluded when either bound is set.
    /// </summary>
    public static ISpecificationBuilder<TEntity> WhereModifiedRange<TEntity>(
        this ISpecificationBuilder<TEntity> query,
        DateTime? modifiedFrom,
        DateTime? modifiedToExclusive)
        where TEntity : BaseEntity
    {
        if (modifiedFrom.HasValue)
        {
            var from = modifiedFrom.Value;
            query = query.Where(x => x.ModifiedAt != null && x.ModifiedAt >= from);
        }

        if (modifiedToExclusive.HasValue)
        {
            var to = modifiedToExclusive.Value;
            if (to == DateTime.MaxValue)
            {
                query = query.Where(x => x.ModifiedAt != null && x.ModifiedAt <= to);
            }
            else
            {
                query = query.Where(x => x.ModifiedAt != null && x.ModifiedAt < to);
            }
        }

        return query;
    }

    /// <summary>
    /// Applies inclusive StartedAt lower bound and exclusive upper bound when present.
    /// Null StartedAt rows are excluded when either bound is set.
    /// </summary>
    public static ISpecificationBuilder<Submission> WhereStartedRange(
        this ISpecificationBuilder<Submission> query,
        DateTime? startedFrom,
        DateTime? startedToExclusive)
    {
        if (startedFrom.HasValue)
        {
            var from = startedFrom.Value;
            query = query.Where(x => x.StartedAt != null && x.StartedAt >= from);
        }

        if (startedToExclusive.HasValue)
        {
            var to = startedToExclusive.Value;
            if (to == DateTime.MaxValue)
            {
                query = query.Where(x => x.StartedAt != null && x.StartedAt <= to);
            }
            else
            {
                query = query.Where(x => x.StartedAt != null && x.StartedAt < to);
            }
        }

        return query;
    }

    /// <summary>
    /// Applies inclusive CompletedAt lower bound and exclusive upper bound when present.
    /// Null CompletedAt rows are excluded when either bound is set.
    /// </summary>
    public static ISpecificationBuilder<Submission> WhereCompletedRange(
        this ISpecificationBuilder<Submission> query,
        DateTime? completedFrom,
        DateTime? completedToExclusive)
    {
        if (completedFrom.HasValue)
        {
            var from = completedFrom.Value;
            query = query.Where(x => x.CompletedAt != null && x.CompletedAt >= from);
        }

        if (completedToExclusive.HasValue)
        {
            var to = completedToExclusive.Value;
            if (to == DateTime.MaxValue)
            {
                query = query.Where(x => x.CompletedAt != null && x.CompletedAt <= to);
            }
            else
            {
                query = query.Where(x => x.CompletedAt != null && x.CompletedAt < to);
            }
        }

        return query;
    }
}
