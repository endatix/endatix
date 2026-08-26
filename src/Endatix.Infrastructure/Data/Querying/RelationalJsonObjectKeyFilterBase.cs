using System.Linq.Expressions;

namespace Endatix.Infrastructure.Data.Querying;

/// <summary>
/// Shared expression plumbing for provider JSON object-key filters.
/// Providers only supply how a key is extracted and how text is LIKE-matched.
/// </summary>
public abstract class RelationalJsonObjectKeyFilterBase : IRelationalJsonObjectKeyFilter
{
    /// <summary>
    /// Whether LIKE patterns must escape SQL Server character-class brackets.
    /// </summary>
    protected abstract bool UsesSqlServerLikeSyntax { get; }

    /// <summary>
    /// Builds the provider call extracting <paramref name="jsonObjectKey"/> as text.
    /// </summary>
    protected abstract Expression ExtractKeyText(Expression jsonProperty, string jsonObjectKey);

    /// <summary>
    /// Builds the provider LIKE / ILIKE call for the given text expression.
    /// </summary>
    protected abstract Expression MatchesPattern(Expression text, string pattern);

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

    private string BuildPattern(string trimmedSearchText, RelationalTextMatchMode matchMode) =>
        RelationalLikePattern.BuildPattern(trimmedSearchText, matchMode, UsesSqlServerLikeSyntax);

    private Expression ExtractKey(ParameterExpression parameter, string jsonPropertyName, string jsonObjectKey) =>
        ExtractKeyText(Expression.Property(parameter, jsonPropertyName), jsonObjectKey);
}
