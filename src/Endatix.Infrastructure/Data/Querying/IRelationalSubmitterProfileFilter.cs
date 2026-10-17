using System.Linq.Expressions;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Infrastructure.Data.Querying;

/// <summary>
/// Applies a <c>submitterProfile.{field}</c> list filter to a submitter profile snapshot column. A value matches
/// exactly, on every provider.
/// </summary>
/// <remarks>
/// <c>:</c> keeps rows whose field equals any value. <c>!:</c> keeps rows whose field equals none of them, including
/// rows with no profile or no such field.
/// </remarks>
public interface IRelationalSubmitterProfileFilter
{
    /// <summary>Filters <paramref name="source"/> by <paramref name="filter"/> on <paramref name="profileSnapshot"/>.</summary>
    /// <param name="source">The query to filter.</param>
    /// <param name="profileSnapshot">The mapped JSON column holding the profile snapshot.</param>
    /// <param name="filter">A <c>submitterProfile.{field}</c> criterion with the <c>:</c> or <c>!:</c> operator.</param>
    /// <returns>The filtered query.</returns>
    /// <typeparam name="TEntity">The type of the rows to filter.</typeparam>
    /// <exception cref="NotSupportedException">The criterion uses another operator.</exception>
    IQueryable<TEntity> Where<TEntity>(
        IQueryable<TEntity> source,
        Expression<Func<TEntity, string?>> profileSnapshot,
        FilterCriterion filter)
        where TEntity : class;
}
