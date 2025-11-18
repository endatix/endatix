namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Request model for partially updating a form.
/// </summary>
public class PartialUpdateFormRequest
{
    /// <summary>
    /// The ID of the form.
    /// </summary>
    public long FormId { get; set; }

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
    /// The ID of the theme to update.
    /// </summary>
    public long? ThemeId { get; set; }

    /// <summary>
    /// The JSON data containing webhook configuration settings for this form.
    /// </summary>
    public string? WebHookSettingsJson { get; set; }
}
