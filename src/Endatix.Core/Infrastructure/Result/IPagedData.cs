namespace Endatix.Core.Infrastructure.Result;

/// <summary>
/// Common paging metadata for paged results.
/// </summary>
public interface IPagedData
{
    int Page { get; }
    int PageSize { get; }
    int TotalRecords { get; }
    int TotalPages { get; }
}
