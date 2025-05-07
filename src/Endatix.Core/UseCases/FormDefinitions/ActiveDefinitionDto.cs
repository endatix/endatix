using Endatix.Core.Configuration;
using Endatix.Core.Entities;

namespace Endatix.Core.UseCases.FormDefinitions;

/// <summary>
/// Data transfer object representing an active form definition with its associated theme data.
/// </summary>
public class ActiveDefinitionDto
{
    public ActiveDefinitionDto(FormDefinition formDefinition, string? themeJsonData = null, IEnumerable<string>? customQuestions = null)
    {
        Id = formDefinition.Id;
        FormId = formDefinition.FormId;
        IsDraft = formDefinition.IsDraft;
        JsonData = formDefinition.JsonData;
        ThemeJsonData = themeJsonData;
        ModifiedAt = formDefinition.ModifiedAt;
        CreatedAt = formDefinition.CreatedAt;
        CustomQuestions = customQuestions ?? Enumerable.Empty<string>();
    }

    /// <summary>
    /// The identifier of the form definition.
    /// </summary>
    public long Id { get; set; }

    /// <summary>
    /// The identifier of the form to which this definition belongs.
    /// </summary>
    public long FormId { get; set; }

    /// <summary>
    /// Indicates whether this form definition is in draft status.
    /// </summary>
    public bool IsDraft { get; set; }

    /// <summary>
    /// The JSON schema data for the form definition.
    /// Defaults to the system's default form definition JSON if not specified.
    /// </summary>
    public string JsonData { get; set; } = "{}";

    /// <summary>
    /// Optional JSON data for the theme associated with this form definition.
    /// </summary>
    public string? ThemeJsonData { get; set; }

    /// <summary>
    /// The timestamp when this form definition was last modified.
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    /// The timestamp when this form definition was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// The list of custom questions' JSON data associated with this form definition's tenant.
    /// </summary>
    public IEnumerable<string> CustomQuestions { get; set; }
}
