namespace Endatix.Api.Endpoints.FormTemplates;

/// <summary>
/// Request model for getting a form template by ID.
/// </summary>
public class GetFormTemplateByIdRequest
{
    /// <summary>
    /// The ID of the form template to get.
    /// </summary>
    public long FormTemplateId { get; set; }
}
