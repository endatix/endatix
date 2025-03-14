namespace Endatix.Api.Endpoints.FormTemplates;

/// <summary>
/// Model of a form template.
/// </summary>
public class FormTemplateModel : FormTemplateModelWithoutJsonData
{
    /// <summary>
    /// The JSON data representing the form template structure.
    /// </summary>
    public string? JsonData { get; set; }
}
