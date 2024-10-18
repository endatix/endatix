using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Assistant.DefineForm;

/// <summary>
/// Command for defining a form using AI assistance.
/// </summary>
public record DefineFormCommand : ICommand<Result<AssistedDefinitionDto>>
{
    public string Prompt { get; init; }
    public string? Definition { get; init; }
    public string? AssistantId { get; init; }
    public string? ThreadId { get; init; }

    public DefineFormCommand(string prompt, string? definition = null, string? assistantId = null, string? threadId = null)
    {
        Guard.Against.NullOrWhiteSpace(prompt);

        Prompt = prompt;
        Definition = definition;
        AssistantId = assistantId;
        ThreadId = threadId;
    }
}
