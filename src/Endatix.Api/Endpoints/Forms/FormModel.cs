using System.Text.Json;

namespace Endatix.Api.Endpoints.Forms;

/// <summary>
/// Model of a form.
/// </summary>
public class FormModel
{
    /// <summary>
    /// The ID of the form.
    /// </summary>
    public string? Id { get; set; }

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
    public bool IsEnabled { get; set; }

    /// <summary>
    /// The ID of the theme associated with the form.
    /// </summary>
    public string? ThemeId { get; set; }

    /// <summary>
    /// The date and time when the form was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// The date and time when the form was last modified.
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    /// The total number of submissions for the form
    /// </summary>
    public int? SubmissionsCount { get; init; }

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
