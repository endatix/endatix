namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Model of a form definition.
/// </summary>
public class FormDefinitionModel
{
    /// <summary>
    /// The ID of the form definition.
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// Indicates if the form definition is a draft.
    /// </summary>
    public bool IsDraft { get; set; }

    /// <summary>
    /// The JSON data of the form definition.
    /// </summary>
    public string? JsonData { get; set; }

    /// <summary>
    /// The ID of the associated form.
    /// </summary>
    public string? FormId { get; set; }


    /// <summary>
    /// The date and time when the form definition was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// The date and time when the form definition was last modified.
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    /// The model of the associated theme stored as a stringified JSON object.
    /// </summary>
    public string? ThemeModel { get; set; }
}
