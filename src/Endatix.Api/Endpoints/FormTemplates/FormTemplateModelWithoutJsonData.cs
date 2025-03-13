namespace Endatix.Api.Endpoints.FormTemplates;

/// <summary>
/// Model of a form template without the JSON data structure.
/// </summary>
public class FormTemplateModelWithoutJsonData
{
    /// <summary>
    /// The ID of the form template.
    /// </summary>
    public string? Id { get; set; }

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
    public bool IsEnabled { get; set; }

    /// <summary>
    /// The date and time when the form template was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// The date and time when the form template was last modified.
    /// </summary>
    public DateTime? ModifiedAt { get; set; }
}
