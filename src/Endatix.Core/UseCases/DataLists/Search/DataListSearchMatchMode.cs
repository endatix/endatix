namespace Endatix.Core.UseCases.DataLists.Search;

/// <summary>
/// How DataList item search matches the resolved label key (not <c>Value</c>).
/// </summary>
public enum DataListSearchMatchMode
{
    /// <summary>
    /// Substring match (current default): <c>%query%</c>.
    /// </summary>
    Contains = 0,

    /// <summary>
    /// Prefix match: <c>query%</c>.
    /// </summary>
    StartsWith = 1,

    /// <summary>
    /// Full-string match (case-insensitive via provider LIKE/ILIKE without wildcards).
    /// </summary>
    Exact = 2
}
