namespace Endatix.Api.Endpoints.CustomQuestions;

/// <summary>
/// Request model for creating a new custom question.
/// </summary>
public class CreateCustomQuestionRequest
{
    /// <summary>
    /// The name of the custom question.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The description of the custom question (optional).
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// The JSON data representing custom question properties.
    /// </summary>
    public string? JsonData { get; set; }
} 