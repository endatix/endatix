namespace Endatix.Core.UseCases.FormDefinitions;

/// <summary>
/// Represents a single field extracted from a form definition schema.
/// </summary>
/// <param name="Name">The field name as defined in the SurveyJS schema (matches the key in submission JsonData).</param>
/// <param name="Title">The human-readable label shown to the respondent.</param>
/// <param name="Type">The SurveyJS element type (e.g. "text", "radiogroup", "checkbox", "file"). Custom question types are returned as-is.</param>
public record DefinitionFieldDto(string Name, string Title, string Type);
