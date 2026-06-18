namespace Endatix.Api.Common;

/// <summary>
/// Paged list request with an optional search term.
/// </summary>
public interface ISearchablePagedRequest : IPageable, ISearchable;
