using System;

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
    /// <returns>A string containing the form definition.</returns>
    string DefineForm(string prompt, string? definition = null);
}
