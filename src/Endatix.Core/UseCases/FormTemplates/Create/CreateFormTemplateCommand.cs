using Ardalis.GuardClauses;
using Endatix.Core.Entities;
using Endatix.Core.Infrastructure.Messaging;
using Endatix.Core.Infrastructure.Result;

namespace Endatix.Core.UseCases.FormTemplates.Create;

/// <summary>
/// Command for creating a form template.
/// </summary>
public record CreateFormTemplateCommand : ICommand<Result<FormTemplate>>
{
    public string Name { get; init; }
    public string? Description { get; init; }
    public string? JsonData { get; init; }
    public bool IsEnabled { get; init; }

    public CreateFormTemplateCommand(string name, string? description, string jsonData, bool isEnabled)
    {
        Guard.Against.NullOrWhiteSpace(name);
        Guard.Against.NullOrWhiteSpace(jsonData);

        Name = name;
        Description = description;
        JsonData = jsonData;
        IsEnabled = isEnabled;
    }
}
