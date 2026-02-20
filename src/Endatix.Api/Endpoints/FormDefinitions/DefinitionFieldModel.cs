namespace Endatix.Api.Endpoints.FormDefinitions;

/// <summary>
/// Model representing a field extracted from a form definition schema.
/// </summary>
public class DefinitionFieldModel
{
    /// <summary>
    /// The field name as defined in the SurveyJS schema (matches the key in submission jsonData).
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// The human-readable label shown to the respondent.
    /// </summary>
    public required string Title { get; set; }

    /// <summary>
    /// The SurveyJS element type (e.g. "text", "radiogroup", "checkbox", "file").
    /// Custom question types are returned as-is.
    /// </summary>
    public required string Type { get; set; }
}
