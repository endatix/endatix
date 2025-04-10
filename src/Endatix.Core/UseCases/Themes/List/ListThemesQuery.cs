using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Themes.List;

/// <summary>
/// Query for retrieving all themes.
/// </summary>
public record ListThemesQuery : IQuery<Result<List<Theme>>>
{
    /// <summary>
    /// The page number to retrieve (optional, for pagination).
    /// </summary>
    public int? Page { get; }
    
    /// <summary>
    /// The number of items per page (optional, for pagination).
    /// </summary>
    public int? PageSize { get; }
    
    /// <summary>
    /// Creates a new instance of ListThemesQuery.
    /// </summary>
    /// <param name="page">Optional page number for pagination.</param>
    /// <param name="pageSize">Optional page size for pagination.</param>
    public ListThemesQuery(int? page = null, int? pageSize = null)
    {
        Page = page;
        PageSize = pageSize;
    }
} 