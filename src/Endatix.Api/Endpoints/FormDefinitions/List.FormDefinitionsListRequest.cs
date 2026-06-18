using Endatix.Api.Common;

namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Request model for listing form definitions.
/// </summary>
public class FormDefinitionsListRequest : IPagedRequest
{
    /// <summary>
    /// The ID of the form.
    /// </summary>
    public long FormId { get; set; }

    /// <summary>
    /// The number of the page
    /// </summary>
    public int? Page { get; set; }

    /// <summary>
    /// The number of items to take.
    /// </summary>
    public int? PageSize { get; set; }
}