using System.Linq.Expressions;

namespace Endatix.Core.Infrastructure.Paging;

/// <summary>
/// EF-translatable predicates for <see cref="UtcDateTimeRange"/> (inclusive From / exclusive To;
/// <see cref="DateTime.MaxValue"/> exclusive To is compared inclusively).
/// </summary>
public static class UtcDateTimeRangeExpressions
{
    /// <summary>
    /// Holds a bound so the built tree reads it as a member access rather than an
    /// <see cref="Expression.Constant(object)"/>. EF Core renders constants as SQL literals and only
    /// parameterizes closure-style member access, so lifting the value keeps one cached query plan
    /// per shape instead of one per requested date.
    /// </summary>
    private sealed class Bound<T>(T value)
    {
        public T Value { get; } = value;
    }

    private static Expression LiftToParameter<T>(T value) =>
        Expression.Property(
            Expression.Constant(new Bound<T>(value)),
            nameof(Bound<T>.Value));

    public static Expression<Func<T, bool>> CompareDateTime<T>(
        Expression<Func<T, DateTime>> keySelector,
        ExpressionType comparisonType,
        DateTime bound)
    {
        var parameter = keySelector.Parameters[0];
        var comparison = Expression.MakeBinary(comparisonType, keySelector.Body, LiftToParameter(bound));
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
        var convertedBound = Expression.Convert(LiftToParameter(bound), typeof(DateTime?));
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
        var convertedBound = Expression.Convert(LiftToParameter(offsetBound), typeof(DateTimeOffset?));
        var comparison = Expression.MakeBinary(comparisonType, property, convertedBound);
        var body = Expression.AndAlso(notNull, comparison);
        return Expression.Lambda<Func<T, bool>>(body, parameter);
    }

    public static ExpressionType ExclusiveToComparison(DateTime exclusiveTo) =>
        exclusiveTo == DateTime.MaxValue
            ? ExpressionType.LessThanOrEqual
            : ExpressionType.LessThan;
}
