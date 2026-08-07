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
        string trimmedSearchText,
        RelationalTextMatchMode matchMode = RelationalTextMatchMode.Contains)
        where TEntity : class
    {
        var pattern = RelationalLikePattern.BuildPattern(trimmedSearchText, matchMode, sqlServerLike: false);
        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var extractCall = CreateExtractCall(parameter, jsonPropertyName, jsonObjectKey);
        var jsonMatch = CreateILikeCall(
            Expression.Constant(EF.Functions),
            extractCall,
            Expression.Constant(pattern),
            Expression.Constant("\\"));

        var lambda = Expression.Lambda<Func<TEntity, bool>>(jsonMatch, parameter);
        return source.Where(lambda);
    }

    /// <inheritdoc />
    public IOrderedQueryable<TEntity> OrderByKey<TEntity>(
        IQueryable<TEntity> source,
        string jsonPropertyName,
        string jsonObjectKey)
        where TEntity : class
    {
        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var extractCall = CreateExtractCall(parameter, jsonPropertyName, jsonObjectKey);
        var keySelector =
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
        var ordered = OrderByKey(source, jsonPropertyName, jsonObjectKey);

        var thenParameter = Expression.Parameter(typeof(TEntity), "e");
        var thenProperty = Expression.Property(thenParameter, thenByPropertyName);
        var thenSelector =
            Expression.Lambda<Func<TEntity, string>>(thenProperty, thenParameter);

        return ordered.ThenBy(thenSelector);
    }

    private static MethodCallExpression CreateExtractCall(
        ParameterExpression parameter,
        string jsonPropertyName,
        string jsonObjectKey)
    {
        var jsonProperty = Expression.Property(parameter, jsonPropertyName);
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
