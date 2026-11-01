using System.Linq.Expressions;
using Endatix.Core.Specifications;
using Endatix.Core.Specifications.Parameters;
using Microsoft.EntityFrameworkCore.Query;

namespace Endatix.Infrastructure.Data.Querying;

/// <summary>
/// Shared shape of <see cref="IRelationalSubmitterProfileFilter"/>: a provider writes the two predicates over a
/// snapshot, and the base applies them to whichever column holds it.
/// </summary>
public abstract class RelationalSubmitterProfileFilterBase : IRelationalSubmitterProfileFilter
{
    /// <summary>Whether the snapshot's <paramref name="field"/> equals any of <paramref name="values"/>.</summary>
    protected abstract Expression<Func<string?, bool>> HasAnyOf(string field, List<string> values);

    /// <summary>
    /// Whether the snapshot's <paramref name="field"/> equals none of <paramref name="values"/>; a missing snapshot or
    /// field counts as none.
    /// </summary>
    protected abstract Expression<Func<string?, bool>> HasNoneOf(string field, List<string> values);

    /// <inheritdoc />
    public IQueryable<TEntity> Where<TEntity>(
        IQueryable<TEntity> source,
        Expression<Func<TEntity, string?>> profileSnapshot,
        FilterCriterion filter)
        where TEntity : class
    {
        var field = SubmissionFilterFields.GetSubmitterProfileFieldName(filter);
        var values = filter.Values.ToList();
        var predicate = filter.Operator switch
        {
            ExpressionType.Equal => HasAnyOf(field, values),
            ExpressionType.NotEqual => HasNoneOf(field, values),
            _ => throw new NotSupportedException($"Operator {filter.Operator} is not supported for submitter profile filters.")
        };

        return source.Where(OnColumn(predicate, profileSnapshot));
    }

    private static Expression<Func<TEntity, bool>> OnColumn<TEntity>(
        Expression<Func<string?, bool>> predicate,
        Expression<Func<TEntity, string?>> profileSnapshot)
    {
        var body = ReplacingExpressionVisitor.Replace(predicate.Parameters[0], profileSnapshot.Body, predicate.Body);
        return Expression.Lambda<Func<TEntity, bool>>(body, profileSnapshot.Parameters);
    }
}
