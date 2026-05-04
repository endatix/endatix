namespace Endatix.Core.Infrastructure.Result;

/// <summary>
/// Interface for paged data.
/// </summary>
public interface IPagedData
{
    /// <summary>
    /// The page number.
    /// </summary>  
    int Page { get; }
    /// <summary>
    /// The page size.
    /// </summary>
    int PageSize { get; }
    
    /// <summary>
    /// The total number of records.
    /// </summary>
    long TotalRecords { get; }

    /// <summary>
    /// The total number of pages.
    /// </summary>
    long TotalPages { get; }
}
