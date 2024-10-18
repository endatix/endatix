using System;
using Endatix.Core.UseCases.Assistant.DefineForm;

namespace Endatix.Core.Abstractions;

/// <summary>
/// Defines operations related to AI assistant functionalities.
/// </summary>
public interface IAssistantService
{
    /// <summary>
    /// Defines a form based on the given prompt and optional existing definition.
    /// </summary>
    /// <param name="prompt">The prompt describing the form to be defined.</param>
    /// <param name="definition">An optional existing definition to be refined or expanded.</param>
    /// <param name="assistantId">An optional ID of the assistant to use for adjusting the form definition.</param>
    /// <param name="threadId">An optional ID of the conversation thread to use for adjusting the form definition.</param>
    /// <returns>An AssistedDefinitionDto containing the form definition and associated IDs.</returns>
    Task<AssistedDefinitionDto> DefineFormAsync(string prompt, string? definition = null, string? assistantId = null, string? threadId = null);
}
