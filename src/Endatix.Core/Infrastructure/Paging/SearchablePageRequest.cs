namespace Endatix.Core.Infrastructure.Paging;

/// <summary>
/// Normalized paging input with an optional free-text search term.
/// </summary>
public sealed record SearchablePageRequest
{
    /// <summary>
    /// The paging information.
    /// </summary>
    public PageRequest Paging { get; }

    /// <summary>
    /// The search term.
    /// </summary>
    public string? Search { get; }

    /// <summary>
    /// Creates a new <see cref="SearchablePageRequest"/> from a page and page size and search term.
    /// </summary>
    /// <param name="page">The page number.</param>
    /// <param name="pageSize">The page size.</param>
    /// <param name="search">The search term.</param>
    /// <returns>A new <see cref="SearchablePageRequest"/>.</returns>
    public SearchablePageRequest(int? page, int? pageSize, string? search)
        : this(new PageRequest(page, pageSize), search)
    {
    }

    /// <summary>
    /// Creates a new <see cref="SearchablePageRequest"/> from a paging information and search term.
    /// </summary>
    /// <param name="paging">The paging information.</param>
    /// <param name="search">The search term.</param>
    /// <returns>A new <see cref="SearchablePageRequest"/>.</returns>
    public SearchablePageRequest(PageRequest paging, string? search)
    {
        Paging = paging;
        Search = NormalizeSearch(search);
    }

    internal static string? NormalizeSearch(string? search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return null;
        }

        var trimmed = search.Trim();
        if (trimmed.Length > PagedRequestLimits.MAX_SEARCH_LENGTH)
        {
            trimmed = trimmed[..PagedRequestLimits.MAX_SEARCH_LENGTH];
        }

        return trimmed;
    }
}
