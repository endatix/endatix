using Endatix.Api.Common;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Request model for listing forms.
/// </summary>
public class FormsListRequest : IPagedRequest, IFilteredRequest
{
    /// <summary>
    /// The number of the page
    /// </summary>
    public int? Page { get; set; }

    /// <summary>
    /// The number of items to take.
    /// </summary>
    public int? PageSize { get; set; }

    /// <summary>
    /// The filter expressions 
    /// </summary>
    public IEnumerable<string>? Filter { get; set; }
}