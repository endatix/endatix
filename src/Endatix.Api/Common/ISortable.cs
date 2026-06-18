using Endatix.Core.Infrastructure.Paging;

namespace Endatix.Api.Common;

/// <summary>
/// Typed sort capability for list requests.
/// </summary>
/// <typeparam name="TSortField">The closed set of sortable fields for the list.</typeparam>
public interface ISortable<TSortField> where TSortField : struct, Enum
{
    /// <summary>
    /// The field to sort by.
    /// </summary>
    TSortField? SortBy { get; set; }

    /// <summary>
    /// The sort direction.
    /// </summary>
    SortDirection? Direction { get; set; }
}
