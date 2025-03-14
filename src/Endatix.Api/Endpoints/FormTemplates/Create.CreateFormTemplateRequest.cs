namespace Endatix.Api.Endpoints.FormTemplates;

/// <summary>
/// Request model for creating a form template.
/// </summary>
public class CreateFormTemplateRequest
{
    /// <summary>
    /// The name of the form template.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Optional description of the form template.
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
