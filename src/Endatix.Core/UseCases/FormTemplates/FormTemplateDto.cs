namespace Endatix.Core.UseCases.FormTemplates;

/// <summary>
/// DTO for Endatix.Core.Entities.FormTemplate class
/// </summary>
public record FormTemplateDto
{
    /// <summary>
    /// Unique identifier for the form template.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// The name of the form template.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Optional description of the form template.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Indicates if the form template is currently enabled.
    /// </summary>
    public bool IsEnabled { get; init; }

    /// <summary>
    /// Timestamp when the form template was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when the form template was last modified, if applicable.
    /// </summary>
    public DateTime? ModifiedAt { get; init; }
}
