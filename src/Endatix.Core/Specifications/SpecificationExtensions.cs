using Ardalis.Specification;
using Endatix.Core.Filters;

namespace Endatix.Core.Specifications;

/// <summary>
/// Extension methods to apply paging for Ardalis.Specification based queries.
/// </summary>
public static class SpecificationExtensions
{
    /// <summary>
    /// Paginate the query based of <see cref="PagingFilter"/> instance
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <param name="query"></param>
    /// <param name="filter"></param>
    /// <returns>Paginate query to be used by the <see cref="ISpecificationBuilder"/></returns>
    public static ISpecificationBuilder<TEntity> Paginate<TEntity>(this ISpecificationBuilder<TEntity> query, PagingFilter filter = null)
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

}
