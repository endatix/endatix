namespace Endatix.Infrastructure.Data.Querying;

/// <summary>
/// Filters and orders by a JSON object string key (e.g. Labels->>'default') with provider LIKE semantics.
/// </summary>
public interface IRelationalJsonObjectKeyFilter
{
    /// <summary>
    /// Filters the query to keep rows where the JSON object key text matches <paramref name="trimmedSearchText"/>.
    /// </summary>
    /// <param name="source">The query to filter.</param>
    /// <param name="jsonPropertyName">The name of the JSON property to filter by.</param>
    /// <param name="jsonObjectKey">The key of the JSON object to filter by.</param>
    /// <param name="trimmedSearchText">The trimmed search text to filter by.</param>
    /// <param name="matchMode">The match mode to use for the search.</param>
    /// <returns>The filtered query.</returns>
    /// <typeparam name="TEntity">The type of the entity to filter.</typeparam>
    IQueryable<TEntity> WhereKeyMatches<TEntity>(
        IQueryable<TEntity> source,
        string jsonPropertyName,
        string jsonObjectKey,
        string trimmedSearchText,
        RelationalTextMatchMode matchMode = RelationalTextMatchMode.Contains)
        where TEntity : class;

    /// <summary>
    /// Filters the query to keep rows where a plain text column or any of the JSON object keys match
    /// <paramref name="trimmedSearchText"/>.
    /// </summary>
    /// <param name="source">The query to filter.</param>
    /// <param name="textPropertyName">The name of the plain text property to match (for example the invariant value).</param>
    /// <param name="jsonPropertyName">The name of the JSON property to filter by.</param>
    /// <param name="jsonObjectKeys">The JSON object keys to match, OR-ed together.</param>
    /// <param name="trimmedSearchText">The trimmed search text to filter by.</param>
    /// <param name="matchMode">The match mode to use for the search.</param>
    /// <returns>The filtered query.</returns>
    /// <typeparam name="TEntity">The type of the entity to filter.</typeparam>
    IQueryable<TEntity> WhereTextOrKeysMatch<TEntity>(
        IQueryable<TEntity> source,
        string textPropertyName,
        string jsonPropertyName,
        IReadOnlyCollection<string> jsonObjectKeys,
        string trimmedSearchText,
        RelationalTextMatchMode matchMode = RelationalTextMatchMode.Contains)
        where TEntity : class;

    /// <summary>
    /// Orders by JSON object key text.
    /// </summary>
    /// <param name="source">The query to order.</param>
    /// <param name="jsonPropertyName">The name of the JSON property to order by.</param>
    /// <param name="jsonObjectKey">The key of the JSON object to order by.</param>
    /// <param name="descending">When true, order descending.</param>
    /// <returns>The ordered query.</returns>
    /// <typeparam name="TEntity">The type of the entity to order.</typeparam>
    IOrderedQueryable<TEntity> OrderByKey<TEntity>(
        IQueryable<TEntity> source,
        string jsonPropertyName,
        string jsonObjectKey,
        bool descending = false)
        where TEntity : class;

    /// <summary>
    /// Orders by JSON object key text (coalesced to <paramref name="thenByPropertyName"/> when missing),
    /// then by <paramref name="thenByPropertyName"/>.
    /// </summary>
    /// <param name="source">The query to order.</param>
    /// <param name="jsonPropertyName">The name of the JSON property to order by.</param>
    /// <param name="jsonObjectKey">The key of the JSON object to order by.</param>
    /// <param name="thenByPropertyName">Fallback for a missing key and the secondary sort property.</param>
    /// <returns>The ordered query.</returns>
    /// <typeparam name="TEntity">The type of the entity to order.</typeparam>
    IOrderedQueryable<TEntity> OrderByKeyThenBy<TEntity>(
        IQueryable<TEntity> source,
        string jsonPropertyName,
        string jsonObjectKey,
        string thenByPropertyName)
        where TEntity : class;
}
