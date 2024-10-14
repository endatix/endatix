using Ardalis.GuardClauses;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.Assistant.DefineForm;

/// <summary>
/// Command for defining a form using AI assistance.
/// </summary>
public record DefineFormCommand : ICommand<Result<string>>
{
    public string Prompt { get; init; }
    public string? Definition { get; init; }

    public DefineFormCommand(string prompt, string? definition = null)
    {
        Guard.Against.NullOrWhiteSpace(prompt);

        Prompt = prompt;
        Definition = definition;
    }
}
