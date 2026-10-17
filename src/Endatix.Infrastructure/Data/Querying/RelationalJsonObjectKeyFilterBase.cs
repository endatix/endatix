using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Endatix.Infrastructure.Data.Querying;

/// <summary>
/// Shared expression plumbing for provider JSON object-key filters.
/// Providers only supply how a key is extracted, how text is LIKE-matched, and which collation orders text by code
/// point.
/// </summary>
/// <remarks>
/// Patterns and bounds are passed as query parameters, so each search reuses one plan instead of compiling a new one
/// per value. Patterns are lowercased: a provider that cannot match without case (SQL Server under a case-sensitive
/// collation) lowercases the value too.
/// </remarks>
public abstract class RelationalJsonObjectKeyFilterBase : IRelationalJsonObjectKeyFilter
{
    private static readonly MethodInfo StringCompareMethod =
        typeof(string).GetMethod(nameof(string.Compare), [typeof(string), typeof(string)])!;

    private static readonly MethodInfo CollateMethod =
        typeof(RelationalDbFunctionsExtensions)
            .GetMethod(nameof(RelationalDbFunctionsExtensions.Collate))!
            .MakeGenericMethod(typeof(string));

    /// <summary>
    /// Whether LIKE patterns must escape SQL Server character-class brackets.
    /// </summary>
    protected abstract bool UsesSqlServerLikeSyntax { get; }

    /// <summary>
    /// A collation that compares text by code point, so range bounds on ISO dates do not depend on the database's
    /// collation (which may ignore the hyphens).
    /// </summary>
    protected abstract string OrdinalCollation { get; }

    /// <summary>
    /// Builds the provider call extracting <paramref name="jsonObjectKey"/> as text.
    /// </summary>
    protected abstract Expression ExtractKeyText(Expression jsonProperty, string jsonObjectKey);

    /// <summary>
    /// Builds the provider case-insensitive LIKE call for the given text expression and lowercased pattern.
    /// </summary>
    protected abstract Expression MatchesPattern(Expression text, Expression lowercasedPattern);

    /// <inheritdoc />
    public IQueryable<TEntity> WhereKeyMatches<TEntity>(
        IQueryable<TEntity> source,
        string jsonPropertyName,
        string jsonObjectKey,
        string trimmedSearchText,
        RelationalTextMatchMode matchMode = RelationalTextMatchMode.Contains)
        where TEntity : class
    {
        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var pattern = BuildPattern(trimmedSearchText, matchMode);
        var body = MatchesPattern(ExtractKey(parameter, jsonPropertyName, jsonObjectKey), pattern);

        return source.Where(Expression.Lambda<Func<TEntity, bool>>(body, parameter));
    }

    /// <inheritdoc />
    public IQueryable<TEntity> WhereTextOrKeysMatch<TEntity>(
        IQueryable<TEntity> source,
        string textPropertyName,
        string jsonPropertyName,
        IReadOnlyCollection<string> jsonObjectKeys,
        string trimmedSearchText,
        RelationalTextMatchMode matchMode = RelationalTextMatchMode.Contains)
        where TEntity : class
    {
        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var pattern = BuildPattern(trimmedSearchText, matchMode);

        var body = MatchesPattern(Expression.Property(parameter, textPropertyName), pattern);
        foreach (var jsonObjectKey in jsonObjectKeys)
        {
            body = Expression.OrElse(
                body,
                MatchesPattern(ExtractKey(parameter, jsonPropertyName, jsonObjectKey), pattern));
        }

        return source.Where(Expression.Lambda<Func<TEntity, bool>>(body, parameter));
    }

    /// <inheritdoc />
    public IQueryable<TEntity> WhereKeyAtLeast<TEntity>(
        IQueryable<TEntity> source,
        JsonObjectKeyPath key,
        string inclusiveLowerBound)
        where TEntity : class =>
        WhereKeyCompares(source, key, new KeyBound(inclusiveLowerBound, Expression.GreaterThanOrEqual));

    /// <inheritdoc />
    public IQueryable<TEntity> WhereKeyBefore<TEntity>(
        IQueryable<TEntity> source,
        JsonObjectKeyPath key,
        string exclusiveUpperBound)
        where TEntity : class =>
        WhereKeyCompares(source, key, new KeyBound(exclusiveUpperBound, Expression.LessThan));

    /// <inheritdoc />
    public IOrderedQueryable<TEntity> OrderByKey<TEntity>(
        IQueryable<TEntity> source,
        string jsonPropertyName,
        string jsonObjectKey,
        bool descending = false)
        where TEntity : class
    {
        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var keySelector = Expression.Lambda<Func<TEntity, string?>>(
            ExtractKey(parameter, jsonPropertyName, jsonObjectKey),
            parameter);

        return descending
            ? source.OrderByDescending(keySelector)
            : source.OrderBy(keySelector);
    }

    /// <inheritdoc />
    public IOrderedQueryable<TEntity> OrderByKeyThenBy<TEntity>(
        IQueryable<TEntity> source,
        string jsonPropertyName,
        string jsonObjectKey,
        string thenByPropertyName)
        where TEntity : class
    {
        // Coalesce missing JSON keys to thenBy so NULL sort order is stable across providers.
        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var fallback = Expression.Property(parameter, thenByPropertyName);
        var primarySelector = Expression.Lambda<Func<TEntity, string>>(
            Expression.Coalesce(ExtractKey(parameter, jsonPropertyName, jsonObjectKey), fallback),
            parameter);

        var thenParameter = Expression.Parameter(typeof(TEntity), "e");
        var thenSelector = Expression.Lambda<Func<TEntity, string>>(
            Expression.Property(thenParameter, thenByPropertyName),
            thenParameter);

        return source.OrderBy(primarySelector).ThenBy(thenSelector);
    }

    // string.Compare(a, b) against 0 is the form EF translates to a SQL comparison of a and b.
    private IQueryable<TEntity> WhereKeyCompares<TEntity>(IQueryable<TEntity> source, JsonObjectKeyPath key, KeyBound bound)
        where TEntity : class
    {
        var parameter = Expression.Parameter(typeof(TEntity), "e");
        var ordinalText = Expression.Call(
            CollateMethod,
            Expression.Constant(EF.Functions),
            ExtractKey(parameter, key.JsonPropertyName, key.JsonObjectKey),
            Expression.Constant(OrdinalCollation));
        var compared = Expression.Call(StringCompareMethod, ordinalText, AsParameter(bound.Value));
        var body = bound.Comparison(compared, Expression.Constant(0));

        return source.Where(Expression.Lambda<Func<TEntity, bool>>(body, parameter));
    }

    private Expression BuildPattern(string trimmedSearchText, RelationalTextMatchMode matchMode) =>
        AsParameter(RelationalLikePattern.BuildPattern(
            trimmedSearchText.ToLowerInvariant(),
            matchMode,
            UsesSqlServerLikeSyntax));

    // EF turns a read of a captured value into a query parameter; a constant would be inlined into the SQL.
    private static Expression AsParameter(string value)
    {
        Expression<Func<string>> captured = () => value;
        return captured.Body;
    }

    private Expression ExtractKey(ParameterExpression parameter, string jsonPropertyName, string jsonObjectKey) =>
        ExtractKeyText(Expression.Property(parameter, jsonPropertyName), jsonObjectKey);

    private sealed record KeyBound(string Value, Func<Expression, Expression, BinaryExpression> Comparison);
}
