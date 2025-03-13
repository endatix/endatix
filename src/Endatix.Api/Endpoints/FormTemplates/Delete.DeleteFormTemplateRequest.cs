namespace Endatix.Api.Endpoints.FormTemplates;

/// <summary>
/// Request model for deleting a form template.
/// </summary>
public class DeleteFormTemplateRequest
{
    /// <summary>
    /// The ID of the form template to delete.
    /// </summary>
    public long FormTemplateId { get; set; }
}
