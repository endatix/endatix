namespace Endatix.Infrastructure.Data.Querying;

/// <summary>
/// Builds a translated substring match for a mapped string column (LIKE / ILIKE semantics depend on the provider implementation).
/// </summary>
public interface IRelationalSubstringLikeFilter
{
    /// <summary>
    /// Filters on a string property by name (via EF.Property).
    /// </summary>
    /// <param name="source">Entity query.</param>
    /// <param name="stringPropertyName">Mapped property name on <typeparamref name="TEntity"/>.</param>
    /// <param name="trimmedSearchText">Non-empty trimmed user input.</param>
    IQueryable<TEntity> WherePropertyMatchesLikeSubstring<TEntity>(
        IQueryable<TEntity> source,
        string stringPropertyName,
        string trimmedSearchText)
        where TEntity : class;
}
