using System.Text.Json;

namespace Endatix.Api.Endpoints.Forms;

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
    /// The JSON data of the active form definition as a string.
    /// </summary>
    [Obsolete("Use FormDefinitionSchema instead.")]
    public string? FormDefinitionJsonData { get; set; }

    /// <summary>
    /// The active form definition schema as a JSON object.
    /// </summary>
    public JsonElement? FormDefinitionSchema { get; set; }

    /// <summary>
    /// The JSON data containing webhook configuration settings for this form as a string.
    /// </summary>
    [Obsolete("Use WebHookSettings instead.")]
    public string? WebHookSettingsJson { get; set; }

    /// <summary>
    /// The webhook configuration settings as a JSON object.
    /// </summary>
    public JsonElement? WebHookSettings { get; set; }
}
