using System.Linq.Expressions;

namespace Endatix.Core.Infrastructure.Paging;

/// <summary>
/// Applies <see cref="UtcDateTimeRange"/> to LINQ / EF <see cref="IQueryable{T}"/> queries
/// (same semantics as Ardalis <c>WhereUtcRange</c> on specifications).
/// </summary>
public static class QueryableUtcDateTimeRangeExtensions
{
    public static IQueryable<T> WhereUtcRange<T>(
        this IQueryable<T> source,
        Expression<Func<T, DateTime>> keySelector,
        UtcDateTimeRange range)
    {
        if (!range.HasBounds)
        {
            return source;
        }

        if (range.InclusiveFrom.HasValue)
        {
            source = source.Where(UtcDateTimeRangeExpressions.CompareDateTime(
                keySelector,
                ExpressionType.GreaterThanOrEqual,
                range.InclusiveFrom.Value));
        }

        if (range.ExclusiveTo.HasValue)
        {
            var to = range.ExclusiveTo.Value;
            source = source.Where(UtcDateTimeRangeExpressions.CompareDateTime(
                keySelector,
                UtcDateTimeRangeExpressions.ExclusiveToComparison(to),
                to));
        }

        return source;
    }

    public static IQueryable<T> WhereUtcRange<T>(
        this IQueryable<T> source,
        Expression<Func<T, DateTime?>> keySelector,
        UtcDateTimeRange range)
    {
        if (!range.HasBounds)
        {
            return source;
        }

        if (range.InclusiveFrom.HasValue)
        {
            source = source.Where(UtcDateTimeRangeExpressions.CompareNullableDateTime(
                keySelector,
                ExpressionType.GreaterThanOrEqual,
                range.InclusiveFrom.Value));
        }

        if (range.ExclusiveTo.HasValue)
        {
            var to = range.ExclusiveTo.Value;
            source = source.Where(UtcDateTimeRangeExpressions.CompareNullableDateTime(
                keySelector,
                UtcDateTimeRangeExpressions.ExclusiveToComparison(to),
                to));
        }

        return source;
    }

    public static IQueryable<T> WhereUtcRange<T>(
        this IQueryable<T> source,
        Expression<Func<T, DateTimeOffset?>> keySelector,
        UtcDateTimeRange range)
    {
        if (!range.HasBounds)
        {
            return source;
        }

        if (range.InclusiveFrom.HasValue)
        {
            source = source.Where(UtcDateTimeRangeExpressions.CompareNullableDateTimeOffset(
                keySelector,
                ExpressionType.GreaterThanOrEqual,
                range.InclusiveFrom.Value));
        }

        if (range.ExclusiveTo.HasValue)
        {
            var to = range.ExclusiveTo.Value;
            source = source.Where(UtcDateTimeRangeExpressions.CompareNullableDateTimeOffset(
                keySelector,
                UtcDateTimeRangeExpressions.ExclusiveToComparison(to),
                to));
        }

        return source;
    }
}
