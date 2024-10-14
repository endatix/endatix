namespace Endatix.Api.Endpoints.Assistant;

/// <summary>
/// Response model for the AI-assisted form definition.
/// </summary>
public class DefineFormResponse
{
    /// <summary>
    /// The AI-generated or refined form definition.
    /// </summary>
    public string Definition { get; set; } = string.Empty;
}
