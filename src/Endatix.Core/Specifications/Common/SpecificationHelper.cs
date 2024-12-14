using System.ComponentModel;
using System.Linq.Expressions;
using System.Reflection;
using Ardalis.GuardClauses;
using Endatix.Core.Specifications.Parameters;

namespace Endatix.Core.Specifications.Common;

/// <summary>
/// Provides helper methods for building and manipulating specifications and filter expressions.
/// </summary>
public static class SpecificationHelper
{
    /// <summary>
    /// Builds a LINQ expression that represents a filter criterion for a given entity type.
    /// </summary>
    /// <typeparam name="TEntity">The type of entity to build the filter expression for.</typeparam>
    /// <param name="filter">The filter criterion containing the field, operator, and values to filter by.</param>
    /// <returns>An expression that can be used to filter entities of type TEntity based on the specified criterion.</returns>
    /// <exception cref="ArgumentNullException">Thrown when the specified field does not exist on the entity type.</exception>
    /// <exception cref="NotSupportedException">Thrown when the specified operator is not supported.</exception>
    public static Expression<Func<TEntity, bool>> BuildFilterExpression<TEntity>(FilterCriterion filter)
    {
        var entityType = typeof(TEntity);
        var propertyInfo = entityType.GetProperty(
            filter.Field,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        Guard.Against.Null(
            propertyInfo,
            nameof(filter.Field),
            $"Property '{filter.Field}' does not exist on type '{entityType.Name}'");

        var entity = Expression.Parameter(entityType, "entity");
        var property = Expression.Property(entity, propertyInfo);

        var expression = filter.Operator switch
        {
            ExpressionType.Equal or
            ExpressionType.NotEqual =>
                BuildEqualityExpression(property, filter.Values, isEqual: filter.Operator == ExpressionType.Equal),

            ExpressionType.GreaterThan or
            ExpressionType.LessThan or
            ExpressionType.GreaterThanOrEqual or
            ExpressionType.LessThanOrEqual =>
                BuildComparisonExpression(property, filter.Values[0], filter.Operator),

            _ => throw new NotSupportedException($"Operator {filter.Operator} is not supported.")
        };

        return Expression.Lambda<Func<TEntity, bool>>(expression, entity);
    }

    private static Expression BuildEqualityExpression(MemberExpression property, IReadOnlyList<string> values, bool isEqual)
    {
        Func<Expression, Expression, Expression> compareOperation = isEqual ? Expression.Equal : Expression.NotEqual;

        // `:` works like `in(value1, value2, ...)` so the values are connected with AND
        // `!:` works like `not in(value1, value2, ...)` so the values are connected with OR
        Func<Expression, Expression, Expression> logicalOperation = isEqual ? Expression.OrElse : Expression.AndAlso;

        return values
            .Select(value => compareOperation(property, CreateConstantExpression(value, property.Type)))
            .Aggregate(logicalOperation);
    }

    private static Expression BuildComparisonExpression(MemberExpression property, string value, ExpressionType @operator)
    {
        var constant = CreateConstantExpression(value, property.Type);

        return @operator switch
        {
            ExpressionType.GreaterThan => Expression.GreaterThan(property, constant),
            ExpressionType.LessThan => Expression.LessThan(property, constant),
            ExpressionType.GreaterThanOrEqual => Expression.GreaterThanOrEqual(property, constant),
            ExpressionType.LessThanOrEqual => Expression.LessThanOrEqual(property, constant),
            _ => throw new NotSupportedException($"Comparison operator {@operator} is not supported.")
        };
    }

    private static ConstantExpression CreateConstantExpression(string value, Type type)
    {
        var parsedValue = ParseValue(value, type);
        return Expression.Constant(parsedValue, type);
    }

    private static object ParseValue(string value, Type targetType)
    {
        if (targetType.IsEnum)
        {
            return Enum.Parse(targetType, value, true);
        }

        return TypeDescriptor.GetConverter(targetType).ConvertFromInvariantString(value)
            ?? throw new ArgumentException($"Cannot convert value '{value}' to type {targetType.Name}");
    }
}
