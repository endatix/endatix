namespace Endatix.Core.UseCases.Forms;

/// <summary>
/// DTO for Endatix.Core.Entities.Form class, including form statistics.
/// </summary>
public record FormDto
{
    /// <summary>
    /// Unique identifier for the form.
    /// </summary>
    public string? Id { get; init; }

    /// <summary>
    /// The name of the form.
    /// </summary>
    public string? Name { get; init; }

    /// <summary>
    /// Optional description of the form.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Indicates if the form is currently enabled.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// The ID of the theme associated with the form.
    /// </summary>
    public string? ThemeId { get; init; }

    /// <summary>
    /// Timestamp when the form was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when the form was last modified, if applicable.
    /// </summary>
    public DateTime? ModifiedAt { get; init; }

    /// <summary>
    /// The total number of submissions for the form
    /// </summary>
    public int? SubmissionsCount { get; init; }

    /// <summary>
    /// The JSON data containing webhook configuration settings for this form.
    /// </summary>
    public string? WebHookSettingsJson { get; init; }
}