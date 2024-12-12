using System.ComponentModel;
using System.Linq.Expressions;
using Ardalis.Specification;
using Endatix.Core.Specifications.Parameters;
using System.Reflection;
using Ardalis.GuardClauses;

namespace Endatix.Core.Specifications;

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
    /// <returns>Paginate query to be used by the <see cref="ISpecificationBuilder"/></returns>
    public static ISpecificationBuilder<TEntity> Paginate<TEntity>(this ISpecificationBuilder<TEntity> query, PagingParameters filter = null)
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
        if (filter.Values == null || !filter.Values.Any())
        {
            return query;
        }

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

        var lambda = Expression.Lambda<Func<TEntity, bool>>(expression, entity);
        return query.Where(lambda);
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
