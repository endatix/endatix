using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.CustomQuestions.Create;

/// <summary>
/// Command for creating a new custom question.
/// </summary>
public record CreateCustomQuestionCommand : ICommand<Result<CustomQuestion>>
{
    /// <summary>
    /// The name of the custom question.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Optional description for the custom question.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// JSON data containing custom question properties.
    /// </summary>
    public string JsonData { get; }

    /// <summary>
    /// Creates a new instance of CreateCustomQuestionCommand.
    /// </summary>
    /// <param name="name">The name of the custom question.</param>
    /// <param name="jsonData">JSON data containing custom question properties.</param>
    /// <param name="description">Optional description for the custom question.</param>
    public CreateCustomQuestionCommand(string name, string jsonData, string? description = null)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.NullOrWhiteSpace(jsonData);

        Name = name;
        JsonData = jsonData;
        Description = description;
    }
} 