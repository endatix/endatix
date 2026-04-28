namespace Endatix.Core.Infrastructure.Result;

/// <summary>
/// Interface for paged data.
/// </summary>
public interface IPagedData
{
    /// <summary>
    /// The page number.
    /// </summary>  
    long Page { get; }
    /// <summary>
    /// The page size.
    /// </summary>
    long PageSize { get; }
    
    /// <summary>
    /// The total number of records.
    /// </summary>
    long TotalRecords { get; }

    /// <summary>
    /// The total number of pages.
    /// </summary>
    long TotalPages { get; }
}
