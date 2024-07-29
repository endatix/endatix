namespace Endatix.Api.Forms;

/// <summary>
/// Request model for creating a form with an active form definition.
/// </summary>
public class CreateFormRequest
{
    /// <summary>
    /// The name of the form.
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// The description of the form.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Indicates if the form is enabled.
    /// </summary>
    public bool? IsEnabled { get; set; }

    /// <summary>
    /// The JSON data of the active form definition.
    /// </summary>
    public string? FormDefinitionJsonData { get; set; }
}
