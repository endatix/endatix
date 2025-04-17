using Endatix.Api.Common;

namespace Endatix.Api.Endpoints.Themes;

/// <summary>
/// Request model for listing themes with optional pagination and filtering.
/// </summary>
public class ListRequest : IPagedRequest, IFilteredRequest
{
    /// <summary>
    /// The page number (1-based).
    /// </summary>
    public int? Page { get; set; }

    /// <summary>
    /// The number of items per page.
    /// </summary>
    public int? PageSize { get; set; }

    /// <summary>
    /// Filter expressions.
    /// </summary>
    public IEnumerable<string>? Filter { get; set; }
} 