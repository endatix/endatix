namespace Endatix.Api.Endpoints.Assistant;

/// <summary>
/// Request model for defining a form using AI assistance.
/// </summary>
public class DefineFormRequest
{
    /// <summary>
    /// The prompt to guide the AI in form definition.
    /// </summary>
    public string Prompt { get; set; } = string.Empty;

    /// <summary>
    /// Optional existing form definition to refine or modify.
    /// </summary>
    public string? Definition { get; set; }

    /// <summary>
    /// Optional assistant ID for continuing a conversation.
    /// </summary>
    public string? AssistantId { get; set; }

    /// <summary>
    /// Optional thread ID for continuing a conversation.
    /// </summary>
    public string? ThreadId { get; set; }
}
