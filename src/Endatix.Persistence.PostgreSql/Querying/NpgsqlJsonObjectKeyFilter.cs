using System.Linq.Expressions;
using Endatix.Infrastructure.Data.Querying;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Persistence.PostgreSql.Querying;

/// <summary>
/// PostgreSQL JSON object-key filter using ILIKE + <c>jsonb_extract_path_text</c>.
/// </summary>
public sealed class NpgsqlJsonObjectKeyFilter : IRelationalJsonObjectKeyFilter
{
    /// <inheritdoc />
    public IQueryable<TEntity> WhereKeyMatches<TEntity>(
        IQueryable<TEntity> source,
        string jsonPropertyName,
        string jsonObjectKey,
        string trimmedSearchText)
        where TEntity : class
    {
        string pattern = RelationalLikePattern.BuildContainsPattern(trimmedSearchText, sqlServerLike: false);
        ParameterExpression parameter = Expression.Parameter(typeof(TEntity), "e");
        MethodCallExpression extractCall = CreateExtractCall(parameter, jsonPropertyName, jsonObjectKey);
        MethodCallExpression jsonMatch = CreateILikeCall(
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
        string pattern = RelationalLikePattern.BuildContainsPattern(trimmedSearchText, sqlServerLike: false);
        ParameterExpression parameter = Expression.Parameter(typeof(TEntity), "e");
        MemberExpression stringProperty = Expression.Property(parameter, stringPropertyName);
        MethodCallExpression extractCall = CreateExtractCall(parameter, jsonPropertyName, jsonObjectKey);

        ConstantExpression patternConstant = Expression.Constant(pattern);
        ConstantExpression escapeConstant = Expression.Constant("\\");
        ConstantExpression functionsConstant = Expression.Constant(EF.Functions);

        MethodCallExpression stringMatch = CreateILikeCall(functionsConstant, stringProperty, patternConstant, escapeConstant);
        MethodCallExpression jsonMatch = CreateILikeCall(functionsConstant, extractCall, patternConstant, escapeConstant);

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
        MethodCallExpression extractCall = CreateExtractCall(parameter, jsonPropertyName, jsonObjectKey);
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

    private static MethodCallExpression CreateExtractCall(
        ParameterExpression parameter,
        string jsonPropertyName,
        string jsonObjectKey)
    {
        MemberExpression jsonProperty = Expression.Property(parameter, jsonPropertyName);
        return Expression.Call(
            NpgsqlJsonDbFunctions.ExtractObjectKeyTextMethod,
            jsonProperty,
            Expression.Constant(jsonObjectKey));
    }

    private static MethodCallExpression CreateILikeCall(
        ConstantExpression functions,
        Expression value,
        ConstantExpression pattern,
        ConstantExpression escape)
    {
        return Expression.Call(
            typeof(NpgsqlDbFunctionsExtensions),
            nameof(NpgsqlDbFunctionsExtensions.ILike),
            typeArguments: Type.EmptyTypes,
            functions,
            value,
            pattern,
            escape);
    }
}
