using System.Text.Json.Serialization;

namespace Endatix.Api.Endpoints.CustomQuestions;

/// <summary>
/// API model representing a custom question.
/// </summary>
public class CustomQuestionModel
{
    /// <summary>
    /// The ID of the custom question.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// The name of the custom question.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The description of the custom question.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The JSON data representing the custom question properties.
    /// </summary>
    public string JsonData { get; set; } = string.Empty;

    /// <summary>
    /// The date and time when the custom question was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// The date and time when the custom question was last modified.
    /// </summary>
    public DateTime? ModifiedAt { get; set; }
}
