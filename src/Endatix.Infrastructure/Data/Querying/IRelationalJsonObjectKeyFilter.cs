namespace Endatix.Infrastructure.Data.Querying;

/// <summary>
/// Filters and orders by a JSON object string key (e.g. Labels->>'default') with provider LIKE semantics.
/// </summary>
public interface IRelationalJsonObjectKeyFilter
{
    /// <summary>
    /// Keeps rows where the JSON object key text matches the substring.
    /// </summary>
    IQueryable<TEntity> WhereKeyMatches<TEntity>(
        IQueryable<TEntity> source,
        string jsonPropertyName,
        string jsonObjectKey,
        string trimmedSearchText)
        where TEntity : class;

    /// <summary>
    /// Keeps rows where <paramref name="stringPropertyName"/> OR the JSON object key text matches the substring
    /// (single translated <c>OR</c> predicate — not two queries).
    /// </summary>
    IQueryable<TEntity> WhereKeyOrPropertyMatches<TEntity>(
        IQueryable<TEntity> source,
        string stringPropertyName,
        string jsonPropertyName,
        string jsonObjectKey,
        string trimmedSearchText)
        where TEntity : class;

    /// <summary>
    /// Orders by JSON object key text.
    /// </summary>
    IOrderedQueryable<TEntity> OrderByKey<TEntity>(
        IQueryable<TEntity> source,
        string jsonPropertyName,
        string jsonObjectKey)
        where TEntity : class;

    /// <summary>
    /// Orders by JSON object key text, then by <paramref name="thenByPropertyName"/>.
    /// </summary>
    IOrderedQueryable<TEntity> OrderByKeyThenBy<TEntity>(
        IQueryable<TEntity> source,
        string jsonPropertyName,
        string jsonObjectKey,
        string thenByPropertyName)
        where TEntity : class;
}
