using Endatix.Api.Common;

namespace Endatix.Api.Endpoints.FormTemplates;

/// <summary>
/// Request model for listing form templates.
/// </summary>
public class FormTemplatesListRequest : IPagedRequest
{
    /// <summary>
    /// The number of the page
    /// </summary>
    public int? Page { get; set; }

    /// <summary>
    /// The number of items to take.
    /// </summary>
    public int? PageSize { get; set; }
}
