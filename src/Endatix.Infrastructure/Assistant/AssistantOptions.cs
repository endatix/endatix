using System.ComponentModel.DataAnnotations;

namespace Endatix.Infrastructure.Assistant;

/// <summary>
/// Configuration options for AI Assistant
/// </summary>
public class AssistantOptions
{
    /// <summary>
    /// The configuration section name where these options are stored.
    /// </summary>
    public const string SECTION_NAME = "Endatix:Assistant";

    /// <summary>
    /// The key used to access the OpenAI API.
    /// This is required and must be set in the configuration.
    /// </summary>
    [Required]
    public required string OpenAiApiKey { get; set; }
}
