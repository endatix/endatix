using System.Linq.Expressions;

namespace Endatix.Core.Infrastructure.Paging;

/// <summary>
/// EF-translatable predicates for <see cref="UtcDateTimeRange"/> (inclusive From / exclusive To;
/// <see cref="DateTime.MaxValue"/> exclusive To is compared inclusively).
/// </summary>
public static class UtcDateTimeRangeExpressions
{
    public static Expression<Func<T, bool>> CompareDateTime<T>(
        Expression<Func<T, DateTime>> keySelector,
        ExpressionType comparisonType,
        DateTime bound)
    {
        var parameter = keySelector.Parameters[0];
        var boundConstant = Expression.Constant(bound, typeof(DateTime));
        var comparison = Expression.MakeBinary(comparisonType, keySelector.Body, boundConstant);
        return Expression.Lambda<Func<T, bool>>(comparison, parameter);
    }

    public static Expression<Func<T, bool>> CompareNullableDateTime<T>(
        Expression<Func<T, DateTime?>> keySelector,
        ExpressionType comparisonType,
        DateTime bound)
    {
        var parameter = keySelector.Parameters[0];
        var property = keySelector.Body;
        var notNull = Expression.NotEqual(property, Expression.Constant(null, typeof(DateTime?)));
        var boundConstant = Expression.Constant(bound, typeof(DateTime));
        var convertedBound = Expression.Convert(boundConstant, typeof(DateTime?));
        var comparison = Expression.MakeBinary(comparisonType, property, convertedBound);
        var body = Expression.AndAlso(notNull, comparison);
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    public static Expression<Func<T, bool>> CompareNullableDateTimeOffset<T>(
        Expression<Func<T, DateTimeOffset?>> keySelector,
        ExpressionType comparisonType,
        DateTime bound)
    {
        var parameter = keySelector.Parameters[0];
        var property = keySelector.Body;
        var notNull = Expression.NotEqual(property, Expression.Constant(null, typeof(DateTimeOffset?)));
        var offsetBound = new DateTimeOffset(DateTime.SpecifyKind(bound, DateTimeKind.Utc));
        var boundConstant = Expression.Constant(offsetBound, typeof(DateTimeOffset));
        var convertedBound = Expression.Convert(boundConstant, typeof(DateTimeOffset?));
        var comparison = Expression.MakeBinary(comparisonType, property, convertedBound);
        var body = Expression.AndAlso(notNull, comparison);
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    public static ExpressionType ExclusiveToComparison(DateTime exclusiveTo) =>
        exclusiveTo == DateTime.MaxValue
            ? ExpressionType.LessThanOrEqual
            : ExpressionType.LessThan;
}
