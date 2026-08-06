using System.Linq.Expressions;
using Endatix.Infrastructure.Data.Querying;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Persistence.SqlServer.Querying;

/// <summary>
/// SQL Server JSON object-key filter using LIKE + <c>JSON_VALUE</c>.
/// </summary>
public sealed class SqlServerJsonObjectKeyFilter : IRelationalJsonObjectKeyFilter
{
    /// <inheritdoc />
    public IQueryable<TEntity> WhereKeyMatches<TEntity>(
        IQueryable<TEntity> source,
        string jsonPropertyName,
        string jsonObjectKey,
        string trimmedSearchText)
        where TEntity : class
    {
        string pattern = RelationalLikePattern.BuildContainsPattern(trimmedSearchText, sqlServerLike: true);
        ParameterExpression parameter = Expression.Parameter(typeof(TEntity), "e");
        MethodCallExpression extractCall = CreateJsonValueCall(parameter, jsonPropertyName, jsonObjectKey);
        MethodCallExpression jsonMatch = CreateLikeCall(
            Expression.Constant(EF.Functions),
            extractCall,
            Expression.Constant(pattern),
            Expression.Constant("\\"));

        Expression<Func<TEntity, bool>> lambda = Expression.Lambda<Func<TEntity, bool>>(jsonMatch, parameter);
        return source.Where(lambda);
    }

    /// <inheritdoc />
    public IQueryable<TEntity> WhereKeyOrPropertyMatches<TEntity>(
        IQueryable<TEntity> source,
        string stringPropertyName,
        string jsonPropertyName,
        string jsonObjectKey,
        string trimmedSearchText)
        where TEntity : class
    {
        string pattern = RelationalLikePattern.BuildContainsPattern(trimmedSearchText, sqlServerLike: true);
        ParameterExpression parameter = Expression.Parameter(typeof(TEntity), "e");
        MemberExpression stringProperty = Expression.Property(parameter, stringPropertyName);
        MethodCallExpression extractCall = CreateJsonValueCall(parameter, jsonPropertyName, jsonObjectKey);

        ConstantExpression patternConstant = Expression.Constant(pattern);
        ConstantExpression escapeConstant = Expression.Constant("\\");
        ConstantExpression functionsConstant = Expression.Constant(EF.Functions);

        MethodCallExpression stringMatch = CreateLikeCall(functionsConstant, stringProperty, patternConstant, escapeConstant);
        MethodCallExpression jsonMatch = CreateLikeCall(functionsConstant, extractCall, patternConstant, escapeConstant);

        BinaryExpression body = Expression.OrElse(stringMatch, jsonMatch);
        Expression<Func<TEntity, bool>> lambda = Expression.Lambda<Func<TEntity, bool>>(body, parameter);
        return source.Where(lambda);
    }

    /// <inheritdoc />
    public IOrderedQueryable<TEntity> OrderByKey<TEntity>(
        IQueryable<TEntity> source,
        string jsonPropertyName,
        string jsonObjectKey)
        where TEntity : class
    {
        ParameterExpression parameter = Expression.Parameter(typeof(TEntity), "e");
        MethodCallExpression extractCall = CreateJsonValueCall(parameter, jsonPropertyName, jsonObjectKey);
        Expression<Func<TEntity, string?>> keySelector =
            Expression.Lambda<Func<TEntity, string?>>(extractCall, parameter);
        return source.OrderBy(keySelector);
    }

    /// <inheritdoc />
    public IOrderedQueryable<TEntity> OrderByKeyThenBy<TEntity>(
        IQueryable<TEntity> source,
        string jsonPropertyName,
        string jsonObjectKey,
        string thenByPropertyName)
        where TEntity : class
    {
        IOrderedQueryable<TEntity> ordered = OrderByKey(source, jsonPropertyName, jsonObjectKey);

        ParameterExpression thenParameter = Expression.Parameter(typeof(TEntity), "e");
        MemberExpression thenProperty = Expression.Property(thenParameter, thenByPropertyName);
        Expression<Func<TEntity, string>> thenSelector =
            Expression.Lambda<Func<TEntity, string>>(thenProperty, thenParameter);

        return ordered.ThenBy(thenSelector);
    }

    private static MethodCallExpression CreateJsonValueCall(
        ParameterExpression parameter,
        string jsonPropertyName,
        string jsonObjectKey)
    {
        MemberExpression jsonProperty = Expression.Property(parameter, jsonPropertyName);
        return Expression.Call(
            SqlServerJsonDbFunctions.JsonValueMethod,
            jsonProperty,
            Expression.Constant(BuildJsonValuePath(jsonObjectKey)));
    }

    /// <summary>
    /// Builds a SQL Server JSON path. Always quotes the key so hyphenated cultures (e.g. <c>en-US</c>) are valid.
    /// </summary>
    public static string BuildJsonValuePath(string jsonObjectKey)
    {
        string escaped = jsonObjectKey
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        return $"$.\"{escaped}\"";
    }

    private static MethodCallExpression CreateLikeCall(
        ConstantExpression functions,
        Expression value,
        ConstantExpression pattern,
        ConstantExpression escape)
    {
        return Expression.Call(
            typeof(DbFunctionsExtensions),
            nameof(DbFunctionsExtensions.Like),
            typeArguments: Type.EmptyTypes,
            functions,
            value,
            pattern,
            escape);
    }
}
