namespace Endatix.Api.Endpoints.Assistant;

/// <summary>
/// Response model for the AI-assisted form definition.
/// </summary>
public class DefineFormResponse
{
    /// <summary>
    /// The human-friendly response from the AI assistant.
    /// </summary>
    public string AssistantResponse { get; set; } = string.Empty;

    /// <summary>
    /// The AI-generated or refined form definition.
    /// </summary>
    public string? Definition { get; set; }

    /// <summary>
    /// The assistant ID for continuing the conversation.
    /// </summary>
    public string AssistantId { get; set; } = string.Empty;

    /// <summary>
    /// The thread ID for continuing the conversation.
    /// </summary>
    public string ThreadId { get; set; } = string.Empty;
}
