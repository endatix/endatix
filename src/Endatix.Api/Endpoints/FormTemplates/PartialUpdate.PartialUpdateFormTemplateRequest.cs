namespace Endatix.Api.Endpoints.FormTemplates;

/// <summary>
/// Request model for updating a form template.
/// </summary>
public class PartialUpdateFormTemplateRequest
{
    /// <summary>
    /// The ID of the form template.
    /// </summary>
    public long FormTemplateId { get; set; }

    /// <summary>
    /// The name of the form template.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The description of the form template.
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Indicates if the form template is enabled.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// The JSON data representing the form template structure.
    /// </summary>
    public string? JsonData { get; set; }
}
